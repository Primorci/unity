import numpy as np
import math
from PIL import Image
import paho.mqtt.client as mqtt
import time

P = np.array([
    [23, 21, 21, 23, 23],
    [24, 22, 22, 20, 24],
    [23, 22, 22, 19, 23],
    [26, 25, 21, 19, 22]
])

# X višina(vrstice), Y širina(stolpci)
X, Y = P.shape

def predictJpegLS(P, X, Y): 
    print(type(P))
    E = [0] * (X * Y)
    
    for x in range(X):
        for y in range(Y):
            if x == 0 and y == 0:
                E[y * X + x] = P[0][0]
            elif x == 0:
                E[y * X + x] = P[0][y - 1] - P[0][y]
            elif y == 0:
                E[y * X + x] = P[x - 1][0] - P[x][0]
            else:
                a = P[x - 1][y] 
                b = P[x][y - 1] 
                c = P[x - 1][y - 1]

                if c >= max(a, b):
                    E[y * X + x] = min(a, b) - P[x][y]
                elif c <= min(a, b):
                    E[y * X + x] = max(a, b) - P[x][y]
                else:
                    E[y * X + x] = (a + b - c) - P[x][y]
                
    return E

def setHeader(f, visina_slike, prvi_element_C, zadnji_element_C, stevilo_elementov):
    B = bytearray()  

    visina_slike_bits = int_to_bits(visina_slike, 12)
    append_to_bin_file(f, visina_slike_bits)
    
    prvi_element_C_bits = int_to_bits(prvi_element_C, 8)
    append_to_bin_file(f, prvi_element_C_bits)
    
    zadnji_element_C_bits = int_to_bits(zadnji_element_C, 32)
    append_to_bin_file(f, zadnji_element_C_bits)
    
    stevilo_elementov_bits = int_to_bits(stevilo_elementov, 24)
    append_to_bin_file(f, stevilo_elementov_bits)
    
    return B

def int_to_bits(number: int, bit_count: int) -> str:
    binary_representation = format(number, f'0{bit_count}b')
    # print(binary_representation)
    return binary_representation

bit_count = 0
curr_byte = 0

def append_to_bin_file(file, bit_string: str):
    global curr_byte, bit_count
    for bit in bit_string:      
        curr_byte = (curr_byte << 1) | int(bit)
        bit_count += 1

        if bit_count == 8:
            file.write(bytes([curr_byte]))
            curr_byte = 0
            bit_count = 0

def enCode(datoteka, B, g, val):
    # Convert 'val' to a binary representation of length 'g'
    binary_representation = bin(val)[2:].zfill(g)

    # Convert each bit in the binary string to an integer and append to B
    B = np.append(B, [int(bit) for bit in binary_representation])

    with open(datoteka, 'ab') as file:
        # Convert binary string to bytes and write
        file.write(int(binary_representation, 2).to_bytes((g + 7) // 8, byteorder='big'))

    return B

def IC(datoteka, B, C, L, H):
    if (H - L) > 1:
        if C[H] != C[L]:
            m = math.floor(0.5 * (H + L))
            g = math.ceil(math.log2(C[H] - C[L] + 1))
            # B = enCode(datoteka, B, g, C[m]) - C[L]
            B = int_to_bits(C[m] - C[L], g)
            append_to_bin_file(datoteka, B)
            if L < m:
                IC(datoteka, B, C, L, m)
            if m < H:
                IC(datoteka, B, C, m, H)
                
def compress(P, X, Y, datoteka):
    E = predictJpegLS(P, X, Y)
    n = X * Y
    N = [0] * len(E)
    N[0] = E[0]
    
    for i in range(1, n):
        if E[i] >= 0:
            N[i] = 2 * E[i]
        else:
            N[i] = 2 * abs(E[i]) - 1
            
    C = [0] * len(N)
    C[0] = N[0]
    for i in range(1, n):
        C[i] = C[i - 1] + N[i]
        
    B = setHeader(datoteka, X, C[0], C[n - 1] , n)
    IC(datoteka, B, C, 0, n - 1)
    
    return B

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
    # print(stevilo_elementov)

    return visina_slike, prvi_element_C, zadnji_element_C, stevilo_elementov

def initializeC(stevilo_el, prvi_el, zadnji_el):
    array = [0] * stevilo_el
    
    array[0] = prvi_el
    array[stevilo_el - 1] = zadnji_el  
    
    return array

def getBits(B, g):
    return B[-g:] if g <= len(B) else B

def DeCode(B):
    binary_str = ''.join(str(bit) for bit in B)
    return int(binary_str, 2)

def DeIC(B, C, L, H):
    if H - L > 1:
        if C[H] == C[L]:
            for i in range(L + 1 ,H):
                C[i] = C[L]
        else:
            m = math.floor(0.5 * (H + L))
            g = math.ceil(math.log2(C[H] - C[L] + 1))            
            C[m] = bit_to_int(B, g) + C[L]
            # print(C[m])
            #print(B)
            # C[m] = tmp + C[L]
            # print(C[m])
            
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
        byte = file.read(1)
        while byte:
            # Convert byte to integer, then format as 8-bit binary string
            bits.extend(format(ord(byte), '08b'))
            byte = file.read(1)
    return [int(bit) for bit in bits]

def decompress(B):
    visina_slike, prvi_element_C, zadnji_element_C, stevilo_elementov = DecodeHeaderFromFile(B)
    
    dolzina_slike = int(stevilo_elementov / visina_slike)
    C = initializeC(stevilo_elementov, prvi_element_C, zadnji_element_C)
    C = DeIC(global_B, C, 0, stevilo_elementov - 1)

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

def read_bmp(image_path):
    with Image.open(image_path) as img:
        # print(f'Format: {img.format}')
        # print(f'Size: {img.size}')
        # print(f'Mode: {img.mode}')
        
        width, height = img.size
        pixels = img.load()  

        pixel_data = [[0] * width for _ in range(height)]
        
        # Read pixel data and store it in the 2D array
        for y in range(height):
            for x in range(width):
                # Access pixels in row-major order
                pixel_data[y][x] = pixels[x, height - 1 - y]  # Flip the y-coordinate
                
        return pixel_data, height, width

def save_bmp_from_pixels(pixel_data, width, height, output_path):
    img = Image.new('L', (width, height))  

    flat_pixel_data = []
    for y in range(height):
        for x in range(width):
            flat_pixel_data.append(pixel_data[height - 1 - y][x])  # Flip the y-coordinate back
    
    img.putdata(flat_pixel_data)

    img.save(output_path, format='BMP')

if __name__ == "__main__":
    file_name = 'E:/Primorci/unity/PRIM-RG/Assets/Screenshots/Compress/compressedR20250119175017773.bin'
    
    # bmp, X, Y = read_bmp("C:/Users/lferk1/Desktop/FAKS/3.1/Multimedia/vaja2/slike_BMP/Lena.bmp")
    # with open(file_name, 'wb') as file:
    #     startTime = time.time()
    #     compress(bmp, X, Y, file)
    #     endTime = time.time()
        
    # print(endTime - startTime)

    global_B = binary_file_to_bits(file_name)
    
    startTime = time.time()
    P_dec, visina, sirina = decompress(global_B)
    endTime = time.time()
        
    save_bmp_from_pixels(P_dec, sirina, visina, "E:/Primorci/YOLO/koncnaAplikacija/decoded_imgs/dec0.bmp")

        
    
    
            
# # import glob
# # import json
# # import os
# # import numpy as np
# # import math
# import glob
# import math
# import os
# import time
# from tkinter import Image
# import numpy as np
# import paho.mqtt.client as mqtt
# # from PIL import Image
# # import time

# import json


# broker = "10.8.1.6"
# port = 1883
# topic = "/YOLO/unity"
# global_pos = 0
# global_bits = 0

# IMAGE_DIR = "E:/Primorci/unity/PRIM-RG/Assets/Screenshots/Compress/"  # Replace with the actual path to your image folder
# SCRAPE_INTERVAL = 0.5  # Interval in seconds

# def on_connect(client, userdata, flags, reason_code, properties):
#     if reason_code.is_failure:
#         print(f"Failed to connect: {reason_code}. loop_forever() will retry connection")
#     else:
#         print("Connected to MQTT Broker successfully.")
#         client.subscribe("/YOLO/bin")

# def on_subscribe(client, userdata, mid, reason_code_list, properties):
#     if reason_code_list[0].is_failure:
#         print(f"Broker rejected you subscription: {reason_code_list[0]}")
#     else:
#         print(f"Broker granted the following QoS: {reason_code_list[0].value}")

# def on_unsubscribe(client, userdata, mid, reason_code_list, properties):
#     if len(reason_code_list) == 0 or not reason_code_list[0].is_failure:
#         print("unsubscribe succeeded (if SUBACK is received in MQTTv3 it success)")
#     else:
#         print(f"Broker replied with failure: {reason_code_list[0]}")
#     client.disconnect()

# def on_message(client, userdata, msg):
#     global rt, playerPos, dangerPos, ps

#     payload = json.loads(msg.payload.decode())

# producer = mqtt.Client(client_id="producer_1", callback_api_version=mqtt.CallbackAPIVersion.VERSION2)

# # Connect to MQTT broker
# try:
#     # Setup MQTT client
#     producer.on_connect = on_connect
#     producer.on_message = on_message
#     producer.on_subscribe = on_subscribe
#     producer.on_unsubscribe = on_unsubscribe

#     producer.connect(broker, port, 60)
#     producer.loop_forever()  # Start a new thread to handle network traffic and dispatching callbacks
# except:
#     print(f"Error: Can not connect to MQTT on port {port}, addr {broker}");

# def bit_to_int(bits, bit_count):
#     global global_pos

#     end = global_pos + bit_count
#     if end > len(bits):
#         end = global_pos + (bit_count - ((global_pos + bit_count) - len(bits)))
    
#     result = 0
    
#     for i in range(global_pos, end):
#         result = (result << 1) | bits[i]
        
#     global_pos += bit_count  
#     return result

# def DecodeHeaderFromFile(bits):
#     visina_slike = bit_to_int(bits, 12)
#     prvi_element_C = bit_to_int(bits, 8)
#     zadnji_element_C = bit_to_int(bits, 32)
#     stevilo_elementov = bit_to_int(bits, 24)

#     return visina_slike, prvi_element_C, zadnji_element_C, stevilo_elementov

# def initializeC(stevilo_el, prvi_el, zadnji_el):
#     array = np.zeros(stevilo_el, dtype=int)
#     # array = [0] * stevilo_el
    
#     array[0] = prvi_el
#     array[stevilo_el - 1] = zadnji_el  
    
#     return array

# def DeIC(B, C, L, H):
#     if H - L > 1:
#         if C[H] == C[L]:
#             for i in range(L + 1 ,H):
#                 C[i] = C[L]
#         else:
#             m = math.floor(0.5 * (H + L))
#             g = math.ceil(math.log2(C[H] - C[L] + 1))            
#             C[m] = bit_to_int(B, g) + C[L]
            
#             if L < m:
#                 DeIC(B, C, L, m)
#             if m < H:
#                 DeIC(B, C, m, H)
                
#     return C
                
# def inversePredictJpegLS(E, X, Y):
#     P = np.zeros((Y, X), dtype=int)

#     P[0, 0] = E[0]
#     P[1:, 0] = np.cumsum(-E[1:, 0])
#     P[0, 1:] = np.cumsum(-E[0, 1:])
    
#     for x in range(1, X):
#         for y in range(1, Y):
#             index = y * X + x
#             if x == 0:
#                 P[x, y] = P[0][y - 1] - E[index]
#             elif y == 0:
#                 P[x, y] = P[x - 1][0] - E[index]
#             else:
#                 a, b, c = P[y - 1, x], P[y, x - 1], P[y - 1, x - 1]

#                 if c >= max(a, b):
#                     P[x, x] = min(a, b) - E[index]
#                 elif c <= min(a, b):
#                     P[X, x] = max(a, b) - E[index]
#                 else:
#                     P[x, x] = a + b - c - E[index]

#     return P

#     # P = [[0] * Y for _ in range(X)]
    
#     # for x in range(X):
#     #     for y in range(Y):
#     #         if x == 0 and y == 0:
#     #             P[x][y] = E[y * X + x]
#     #         elif x == 0:
#     #             P[x][y] = P[0][y - 1] - E[y * X + x]
#     #         elif y == 0:
#     #             P[x][y] = P[x - 1][0] - E[y * X + x]
#     #         else:
#     #             a = P[x - 1][y]
#     #             b = P[x][y - 1]
#     #             c = P[x - 1][y - 1]

#     #             if c >= max(a, b):
#     #                 P[x][y] = min(a, b) - E[y * X + x]
#     #             elif c <= min(a, b):
#     #                 P[x][y] = max(a, b) - E[y * X + x]
#     #             else:
#     #                 P[x][y] = (a + b - c) - E[y * X + x]
                    
#     # return P

# # def binary_file_to_bits(file_path):
# #     bits = []
# #     with open(file_path, 'rb') as file:
# #         byte = file.read(1)
# #         while byte:
# #             # Convert byte to integer, then format as 8-bit binary string
# #             bits.extend(format(ord(byte), '08b'))
# #             byte = file.read(1)
# #     return [int(bit) for bit in bits]

# def binary_file_to_bits(file_path):
#     bits = []
#     with open(file_path, 'rb') as file:
#         byte = file.read(1)
#         while byte:
#             # Convert byte to integer, then format as 8-bit binary string
#             bits.extend(format(ord(byte), '08b'))
#             byte = file.read(1)
#     return [int(bit) for bit in bits]


# def decompress(B):
#     visina_slike, prvi_element_C, zadnji_element_C, stevilo_elementov = DecodeHeaderFromFile(B)

    
#     dolzina_slike = int(stevilo_elementov / visina_slike)
#     C = initializeC(stevilo_elementov, prvi_element_C, zadnji_element_C)
#     C = DeIC(global_bits, C, 0, stevilo_elementov - 1)

#     N = [0] * len(C)
#     N[0] = C[0]
#     for i in range(1, stevilo_elementov):
#         N[i] = C[i] - C[i - 1]
        

#     E = [0] * len(N)
#     E[0] = N[0]
#     for i in range(1, stevilo_elementov):
#         if N[i] % 2 == 0:
#             E[i] = int(N[i] / 2)
#         else:
#             E[i] = int(-(N[i] + 1) / 2)

#     return inversePredictJpegLS(E, visina_slike, dolzina_slike), visina_slike, dolzina_slike

# def save_bmp_from_pixels(pixel_data, width, height, output_path):
#     img = Image.new('L', (width, height))  

#     flat_pixel_data = []
#     for y in range(height):
#         for x in range(width):
#             flat_pixel_data.append(pixel_data[height - 1 - y][x])  # Flip the y-coordinate back
    
#     img.putdata(flat_pixel_data)

#     img.save(output_path, format='BMP')

# def scrape_images():
#     global global_pos
#     print("Starting bin scraping...")
#     processed_files = set()

#     while True:
#         bin_files = glob.glob(os.path.join(IMAGE_DIR, "*.bin"))
#         for bin_path in bin_files:
#             if bin_path not in processed_files:
#                 print(f"Processing bin: {bin_path}")

#                 try:
#                     global_bits = binary_file_to_bits(bin_path)
#                     P_dec, visina, sirina = decompress(global_bits)
#                     save_bmp_from_pixels(P_dec, sirina, visina, f"imgs/dec_{len(processed_files)}.bmp")
#                     processed_files.add(bin_path)
#                     global_bits = 0
#                     global_pos = 0
#                 except Exception as e:
#                     print(f"Error processing {bin_path}: {e}")

#         time.sleep(SCRAPE_INTERVAL)

# if __name__ == "__main__":
#     scrape_images()
# import math
# from PIL import Image
# import time

# pos = 0

# def bit_to_int(bits, bit_count):
#     global pos
#     end = pos + bit_count
#     if end > len(bits):
#         end = pos + (bit_count - ((pos + bit_count) - len(bits)))
    
#     result = 0
    
#     for i in range(pos, end):
#         result = (result << 1) | bits[i]
        
#     pos += bit_count  
#     return result

# def DecodeHeaderFromFile(bits):
#     visina_slike = bit_to_int(bits, 12)
#     prvi_element_C = bit_to_int(bits, 8)
#     zadnji_element_C = bit_to_int(bits, 32)
#     stevilo_elementov = bit_to_int(bits, 24)

#     return visina_slike, prvi_element_C, zadnji_element_C, stevilo_elementov

# def initializeC(stevilo_el, prvi_el, zadnji_el):
#     array = [0] * stevilo_el
    
#     array[0] = prvi_el
#     array[stevilo_el - 1] = zadnji_el  
    
#     return array

# def DeIC(B, C, L, H):
#     if H - L > 1:
#         if C[H] == C[L]:
#             for i in range(L + 1 ,H):
#                 C[i] = C[L]
#         else:
#             m = math.floor(0.5 * (H + L))
#             g = math.ceil(math.log2(C[H] - C[L] + 1))            
#             C[m] = bit_to_int(B, g) + C[L]
            
#             if L < m:
#                 DeIC(B, C, L, m)
#             if m < H:
#                 DeIC(B, C, m, H)
                
#     return C
                
# def inversePredictJpegLS(E, X, Y):
#     P = [[0] * Y for _ in range(X)]
    
#     for x in range(X):
#         for y in range(Y):
#             if x == 0 and y == 0:
#                 P[x][y] = E[y * X + x]
#             elif x == 0:
#                 P[x][y] = P[0][y - 1] - E[y * X + x]
#             elif y == 0:
#                 P[x][y] = P[x - 1][0] - E[y * X + x]
#             else:
#                 a = P[x - 1][y]
#                 b = P[x][y - 1]
#                 c = P[x - 1][y - 1]

#                 if c >= max(a, b):
#                     P[x][y] = min(a, b) - E[y * X + x]
#                 elif c <= min(a, b):
#                     P[x][y] = max(a, b) - E[y * X + x]
#                 else:
#                     P[x][y] = (a + b - c) - E[y * X + x]
                    
#     return P

# def binary_file_to_bits(file_path):
#     bits = []
#     with open(file_path, 'rb') as file:
#         byte = file.read(1)
#         while byte:
#             # Convert byte to integer, then format as 8-bit binary string
#             bits.extend(format(ord(byte), '08b'))
#             byte = file.read(1)
#     return [int(bit) for bit in bits]

# def decompress(B):
#     visina_slike, prvi_element_C, zadnji_element_C, stevilo_elementov = DecodeHeaderFromFile(B)
    
#     dolzina_slike = int(stevilo_elementov / visina_slike)
#     C = initializeC(stevilo_elementov, prvi_element_C, zadnji_element_C)
#     C = DeIC(global_B, C, 0, stevilo_elementov - 1)

#     N = [0] * len(C)
#     N[0] = C[0]
#     for i in range(1, stevilo_elementov):
#         N[i] = C[i] - C[i - 1]
        

#     E = [0] * len(N)
#     E[0] = N[0]
#     for i in range(1, stevilo_elementov):
#         if N[i] % 2 == 0:
#             E[i] = int(N[i] / 2)
#         else:
#             E[i] = int(-(N[i] + 1) / 2)

#     return inversePredictJpegLS(E, visina_slike, dolzina_slike), visina_slike, dolzina_slike

# def save_bmp_from_pixels(pixel_data, width, height, output_path):
#     img = Image.new('L', (width, height))  

#     flat_pixel_data = []
#     for y in range(height):
#         for x in range(width):
#             flat_pixel_data.append(pixel_data[height - 1 - y][x])  # Flip the y-coordinate back
    
#     img.putdata(flat_pixel_data)

#     img.save(output_path, format='BMP')

# if __name__ == "__main__":
#     file_name = 'E:/Primorci/unity/PRIM-RG/Assets/Screenshots/Compress/compressedR28,66262.bin'

#     global_B = binary_file_to_bits(file_name)
    
#     startTime = time.time()
#     P_dec, visina, sirina = decompress(global_B)
#     endTime = time.time()
    
#     print(endTime - startTime)
#     print(P_dec)
        
#     save_bmp_from_pixels(P_dec, sirina, visina, "imgs/dec.bmp")