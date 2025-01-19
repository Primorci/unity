import argparse
import glob
import json
import os
import numpy as np
import math
import paho.mqtt.client as mqtt
from PIL import Image
import time

broker = "10.8.1.6"
port = 1883
topic = "/YOLO/unity"

def on_connect(client, userdata, flags, reason_code, properties):
    if reason_code.is_failure:
        print(f"Failed to connect: {reason_code}. loop_forever() will retry connection")
    else:
        print("Connected to MQTT Broker successfully.")
        client.subscribe("/YOLO/bin")

def on_subscribe(client, userdata, mid, reason_code_list, properties):
    if reason_code_list[0].is_failure:
        print(f"Broker rejected you subscription: {reason_code_list[0]}")
    else:
        print(f"Broker granted the following QoS: {reason_code_list[0].value}")

def on_unsubscribe(client, userdata, mid, reason_code_list, properties):
    if len(reason_code_list) == 0 or not reason_code_list[0].is_failure:
        print("unsubscribe succeeded (if SUBACK is received in MQTTv3 it success)")
    else:
        print(f"Broker replied with failure: {reason_code_list[0]}")
    client.disconnect()

def on_message(client, userdata, msg):
    global rt, playerPos, dangerPos, ps

    payload = json.loads(msg.payload.decode())
   

producer = mqtt.Client(client_id="decodeProducer", callback_api_version=mqtt.CallbackAPIVersion.VERSION2)

# Connect to MQTT broker
try:
    # Setup MQTT client
    producer.on_connect = on_connect
    producer.on_message = on_message
    producer.on_subscribe = on_subscribe
    producer.on_unsubscribe = on_unsubscribe

    producer.connect(broker, port, 60)
    producer.loop_start()  # Start a new thread to handle network traffic and dispatching callbacks
except:
    print(f"Error: Can not connect to MQTT on port {port}, addr {broker}")

pos = 0

def bit_to_int(bits, bit_count):
    global pos
    end = pos + bit_count
    if end > len(bits):
        end = pos + (bit_count - ((pos + bit_count) - len(bits)))
    
    result = 0
    
    for i in range(pos, end):
        result = (result << 1) | bits[i]
        
    pos += bit_count  
    return result

def DecodeHeaderFromFile(bits):
    visina_slike = bit_to_int(bits, 12)
    prvi_element_C = bit_to_int(bits, 8)
    zadnji_element_C = bit_to_int(bits, 32)
    stevilo_elementov = bit_to_int(bits, 24)

    return visina_slike, prvi_element_C, zadnji_element_C, stevilo_elementov

def initializeC(stevilo_el, prvi_el, zadnji_el):
    array = [0] * stevilo_el
    
    array[0] = prvi_el
    array[stevilo_el - 1] = zadnji_el  
    
    return array

def DeIC(B, C, L, H):
    if H - L > 1:
        if C[H] == C[L]:
            for i in range(L + 1 ,H):
                C[i] = C[L]
        else:
            m = math.floor(0.5 * (H + L))
            g = math.ceil(math.log2(C[H] - C[L] + 1))            
            C[m] = bit_to_int(B, g) + C[L]
            
            if L < m:
                DeIC(B, C, L, m)
            if m < H:
                DeIC(B, C, m, H)
                
    return C
                
def inversePredictJpegLS(E, X, Y):
    P = [[0] * Y for _ in range(X)]
    
    for x in range(X):
        for y in range(Y):
            if x == 0 and y == 0:
                P[x][y] = E[y * X + x]
            elif x == 0:
                P[x][y] = P[0][y - 1] - E[y * X + x]
            elif y == 0:
                P[x][y] = P[x - 1][0] - E[y * X + x]
            else:
                a = P[x - 1][y]
                b = P[x][y - 1]
                c = P[x - 1][y - 1]

                if c >= max(a, b):
                    P[x][y] = min(a, b) - E[y * X + x]
                elif c <= min(a, b):
                    P[x][y] = max(a, b) - E[y * X + x]
                else:
                    P[x][y] = (a + b - c) - E[y * X + x]
                    
    return P

def binary_file_to_bits(file_path):
    bits = []
    with open(file_path, 'rb') as file:
        while chunk := file.read(1024):  # Read in chunks for better performance
            for byte in chunk:
                bits.extend(format(byte, '08b'))  # Convert each byte to an 8-bit binary string
    return np.array([int(bit) for bit in bits])

def decompress(B):
    visina_slike, prvi_element_C, zadnji_element_C, stevilo_elementov = DecodeHeaderFromFile(B)
    
    dolzina_slike = int(stevilo_elementov / visina_slike)
    C = initializeC(stevilo_elementov, prvi_element_C, zadnji_element_C)
    C = DeIC(B, C, 0, stevilo_elementov - 1)

    N = [0] * len(C)
    N[0] = C[0]
    for i in range(1, stevilo_elementov):
        N[i] = C[i] - C[i - 1]
        

    E = [0] * len(N)
    E[0] = N[0]
    for i in range(1, stevilo_elementov):
        if N[i] % 2 == 0:
            E[i] = int(N[i] / 2)
        else:
            E[i] = int(-(N[i] + 1) / 2)

    return inversePredictJpegLS(E, visina_slike, dolzina_slike), visina_slike, dolzina_slike

def save_bmp_from_pixels(pixel_data, width, height, output_path):
    img = Image.new('L', (width, height))  

    flat_pixel_data = []
    for y in range(height):
        for x in range(width):
            flat_pixel_data.append(pixel_data[height - 1 - y][x])  # Flip the y-coordinate back
    
    img.putdata(flat_pixel_data)

    img.save(output_path, format='BMP')
    print(f"Saved image as BMP file in {output_path}")

def scrape_images():
    global pos

    print("Starting bin scraping...")
    processed_files = set()

    while True:
        bin_files = glob.glob(os.path.join("E:/Primorci/unity/PRIM-RG/Assets/Screenshots/Compress/", "*.bin"))
        for bin_path in bin_files:
            if bin_path not in processed_files:
                print(f"Processing bin: {bin_path}")

                global_B = binary_file_to_bits(bin_path)
                P_dec, visina, sirina = decompress(global_B)
                save_bmp_from_pixels(P_dec, sirina, visina, f"imgs/dec_{len(processed_files)}.bmp")
                processed_files.add(bin_path)
                global_B = None
                pos = 0

        time.sleep(1)

# parser = argparse.ArgumentParser(description="Decompress images from binary files.")
# parser.add_argument("--bin_file", help="Path to the binary file")
# parser.add_argument("--count", help="Count of files to process")

# args = parser.parse_args()

# print(f"Processing file: {args.bin_file}")
# print(f"Count: {args.count}")

if __name__ == "__main__":
    scrape_images()
    # file_name = 'E:/Primorci/unity/PRIM-RG/Assets/Screenshots/Compress/compressedR6,060938.bin'
    # if args.bin_file is not None:
    #     global_B = binary_file_to_bits(args.bin_file)
        
    #     # startTime = time.time()
    #     P_dec, visina, sirina = decompress(global_B)
    #     # endTime = time.time()
        
    #     # print(endTime - startTime)
    #     # print(P_dec)
            
    #     save_bmp_from_pixels(P_dec, sirina, visina, f"decoded_imgs/dec{args.count}.bmp")