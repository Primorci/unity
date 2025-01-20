from concurrent.futures import ThreadPoolExecutor
import math
import os
import time
import glob
import torch
import cv2
import numpy as np
from PIL import Image
import paho.mqtt.client as mqtt
import json

# Configure environment
os.environ['KMP_DUPLICATE_LIB_OK'] = 'TRUE'

# Load the models
modelDanger = torch.hub.load('ultralytics/yolov5', 'custom', path='Assets/Python/koncnaAplikacija/Yolo/DangerBest.pt')
modelRoad = torch.hub.load('ultralytics/yolov5', 'custom', path='Assets/Python/koncnaAplikacija/Yolo/RoadBest.pt')

# Define the directory to scrape for images
# IMAGE_DIR = "imgs/"
IMAGE_DIR = "Assets/Screenshots/Raw" 
SCRAPE_INTERVAL = 1  

broker = "10.8.1.6"
port = 1883
topic = "/YOLO/unity"
checkTopic = "/YOLO/unity/check"

rt : str = None
playerPos : dict = None
playerSpeed : float = 0.0
dangerPos = []

def on_connect(client, userdata, flags, reason_code, properties):
    if reason_code.is_failure:
        print(f"Failed to connect: {reason_code}. loop_forever() will retry connection")
    else:
        print("Connected to MQTT Broker successfully.")
        topics = [("game/road/generation", 0), ("game/road/danger", 0), ("game/player", 0)]
        client.subscribe(topics)

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
    global rt, playerPos, dangerPos, d, playerSpeed

    payload = json.loads(msg.payload.decode())

    if msg.topic == "game/road/generation":
        rt = payload.get("roadType", None)
        # print(type(rt))
    elif msg.topic == "game/road/danger":
        d = payload.get("isDanger", False)
        # dangerPos = payload.get("position", 0.0)
        dangerPos.insert(0, payload.get("position", 0.0))
        if len(dangerPos) > 4:
            dangerPos.pop(len(playerPos))
        # print(type(dangerPos))
    elif msg.topic == "game/player":
        playerPos = payload.get("position", 0.0)
        playerSpeed = payload.get("speed", 0.0)
        # print(type(playerPos))

producer = mqtt.Client(client_id="backendProducer", callback_api_version=mqtt.CallbackAPIVersion.VERSION2)

# Connect to MQTT broker
try:
    # Setup MQTT client
    producer.on_connect = on_connect
    producer.on_message = on_message
    producer.on_subscribe = on_subscribe
    producer.on_unsubscribe = on_unsubscribe

    producer.connect(broker, port, 60)
    producer.loop_start()
except:
    print(f"Error: Can not connect to MQTT on port {port}, addr {broker}");

d = False

def process_image(image_path):
    image = cv2.imread(image_path)
    if image is None:
        print(f"Failed to read image: {image_path}")
        return json.dumps({"error": f"Failed to read image: {image_path}"})

    height, width = image.shape[:2]

    # Check if the image is 800x600
    if (width, height) != (800, 600):
        # print(f"Image resolution is {width}x{height}. Resizing to 800x600.")
        image = cv2.resize(image, (800, 600))
        cv2.imwrite("resized_image.jpg", image)

    image_rgb = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
    
    results_danger = modelDanger(image_rgb)
    results_road = modelRoad(image_rgb)
    
    detected_danger = []
    
    if results_danger.pred[0] is not None:
        detected_danger.extend(
            results_danger.names[int(cls)] for cls in results_danger.pred[0][:, -1]
        )

    largest_road_area = 0
    largest_road_type = None

    if results_road.pred[0] is not None:
        for bbox in results_road.xyxy[0]:  # Loop through bounding boxes
            x_min, y_min, x_max, y_max, conf, cls = bbox[:6]  # Unpack bounding box data
            cls = int(cls)  # Ensure the class index is an integer
            area = (x_max - x_min) * (y_max - y_min)  # Calculate area
            if area > largest_road_area:  # Update if this is the largest box
                largest_road_area = area
                largest_road_type = results_road.names[cls]  # Access the correct class name

    # Calculate distance
    try:
        distance = calculate_distance(playerPos, dangerPos)
        # Rank the proximity
        proximity_rank = rank_distance(distance, 6, 10)
        print(distance, proximity_rank)
    except Exception as e:
        print(e)

    try:
        result = {
            "detected_danger": True if len(detected_danger) > 0 or d else False,
            "distance_danger": proximity_rank,
            "road_type": [largest_road_type, rt]
        }
    except:
        result = {
            "detected_danger": True if len(detected_danger) > 0 or d else False,
            "distance_danger": "error",
            "road_type": largest_road_type
        }
    
    return json.dumps(result, indent=4)

def calculate_distance(playerPos: dict, dangerPos: list) -> float:
    if not playerPos or not dangerPos:
        return float('inf')
    
    player_x, player_y, player_z = playerPos["x"], playerPos["y"], playerPos["z"]
    least_distance = float('inf')
    
    for danger in dangerPos:
        danger_x, danger_y, danger_z = danger["x"], danger["y"], danger["z"]
        distance = math.sqrt(
            (danger_x - player_x) ** 2 +
            (danger_y - player_y) ** 2 +
            (danger_z - player_z) ** 2
        )
        least_distance = min(least_distance, distance)
    
    return least_distance

def rank_distance(distance, low_threshold, high_threshold):
    speed_factor = 1 + (playerSpeed / 10) 

    adjusted_low_threshold = low_threshold + speed_factor
    adjusted_high_threshold = high_threshold + speed_factor
    print(f"LOW: {adjusted_low_threshold}, HIGH: {adjusted_high_threshold}")

    if distance <= adjusted_low_threshold:
        return 2 
    elif distance <= adjusted_high_threshold:
        return 1
    else:
        return 0 

# def scrape_images():
#     print("Starting image scraping...")
#     processed_files = set()
    
#     while True:
#         print(playerSpeed)
#         # Get a list of all .jpg and .png files in the directory
#         image_files = glob.glob(os.path.join(IMAGE_DIR, "*.bmp")) + glob.glob(os.path.join(IMAGE_DIR, "*.jpg")) + glob.glob(os.path.join(IMAGE_DIR, "*.png"))
        
#         for image_path in image_files:
#             if image_path not in processed_files:
#                 # print(f"Processing image: {image_path}")

#                 results_detected_json = process_image(image_path)

#                 results_detected = json.loads(results_detected_json)

#                 if results_detected:
#                     print(f"Detected objects in {image_path}: {results_detected}")
#                 else:
#                     print(f"No results detected for {image_path}")

#                 try:
#                     producer.publish(topic, results_detected_json, qos=1, retain=False)
#                 except:
#                     print(f"MQTT cant send data type {type(results_detected_json)}")
#                 processed_files.add(image_path)
        
#         time.sleep(SCRAPE_INTERVAL)
def scrape_images():
    print("Starting image scraping...")
    processed_files = set()
    
    with ThreadPoolExecutor(max_workers=5) as executor: 
        while True:
            image_files = glob.glob(os.path.join(IMAGE_DIR, "*.bmp")) + glob.glob(os.path.join(IMAGE_DIR, "*.jpg")) + glob.glob(os.path.join(IMAGE_DIR, "*.png"))
            
            for image_path in image_files:
                if image_path not in processed_files:
                    # Submit the image processing task to the executor
                    executor.submit(process_and_publish, image_path)
                    processed_files.add(image_path)
                    
            time.sleep(SCRAPE_INTERVAL)

def process_and_publish(image_path):
    """This function is responsible for processing images and publishing the result."""
    results_detected_json = process_image(image_path)
    
    results_detected = json.loads(results_detected_json)
    
    if results_detected:
        print(f"Detected objects in {image_path}: {results_detected}")
    else:
        print(f"No results detected for {image_path}")
    
    try:
        producer.publish(topic, results_detected_json, qos=1, retain=False)
    except:
        print(f"MQTT cant send data type {type(results_detected_json)}")

if __name__ == "__main__":
    scrape_images()
