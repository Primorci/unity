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
modelDanger = torch.hub.load('ultralytics/yolov5', 'custom', path='Yolo/DangerBest.pt')
modelRoad = torch.hub.load('ultralytics/yolov5', 'custom', path='Yolo/RoadBest.pt')

# Define the directory to scrape for images
IMAGE_DIR = "imgs/"  # Replace with the actual path to your image folder
SCRAPE_INTERVAL = 1  # Interval in seconds

broker = "10.8.1.6"
port = 1883
topic = "/YOLO/unity"
checkTopic = "/YOLO/unity/check"

rt : str = None
playerPos : dict = None
dangerPos : dict = None

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
    global rt, playerPos, dangerPos, d

    payload = json.loads(msg.payload.decode())

    if msg.topic == "game/road/generation":
        rt = payload.get("roadType", None)
        # print(type(rt))
    elif msg.topic == "game/road/danger":
        d+=1
        dangerPos = payload.get("position", 0.0)
        # print(type(dangerPos))
    elif msg.topic == "game/player":
        playerPos = payload.get("position", 0.0)
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
    producer.loop_start()  # Start a new thread to handle network traffic and dispatching callbacks
except:
    print(f"Error: Can not connect to MQTT on port {port}, addr {broker}");

d = 0
d_prev = d

def process_image(image_path):
    image = cv2.imread(image_path)
    if image is None:
        print(f"Failed to read image: {image_path}")
        return json.dumps({"error": f"Failed to read image: {image_path}"})
    
     # Get the current dimensions (height, width, channels)
    height, width = image.shape[:2]

    # Check if the image is 800x600
    if (width, height) != (800, 600):
        print(f"Image resolution is {width}x{height}. Resizing to 800x600.")
        # Resize the image to 800x600
        image = cv2.resize(image, (800, 600))
        # Save the resized image (you can change the name or path as needed)
        cv2.imwrite("resized_image.jpg", image)

    image_rgb = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
    
    # Run YOLO models
    results_danger = modelDanger(image_rgb)
    results_road = modelRoad(image_rgb)
    
    # Collect detected objects
    detected_danger = []
    
    # Process Danger model results
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
        distance = calculate_distance(dangerPos, playerPos)
        # Rank the proximity
        low_threshold = 6   # Example low threshold (adjust as needed)
        high_threshold = 10   # Example high threshold (adjust as needed)
        proximity_rank = rank_distance(distance, low_threshold, high_threshold)
    except Exception as e:
        print(e)

    # Format results as a dictionary
    try:
        result = {
            "detected_danger": True if len(detected_danger) > 0 or d != d_prev else False,
            "distance_danger": proximity_rank,
            "road_type": [largest_road_type, rt]
        }
    except:
        result = {
            "detected_danger": True if len(detected_danger) > 0 or d != d_prev else False,
            "distance_danger": "error",
            "road_type": largest_road_type
        }
    
    # Convert the dictionary to a JSON string
    return json.dumps(result, indent=4)

def calculate_distance(pos1, pos2):
    return math.sqrt((pos2["x"] - pos1["x"])**2 + 
                     (pos2["y"] - pos1["y"])**2 + 
                     (pos2["z"] - pos1["z"])**2)

def rank_distance(distance, low_threshold, high_threshold):
    """
    Rank the distance as low, mid, or high based on thresholds.
    """
    if distance <= low_threshold:
        return 2
    elif distance <= high_threshold:
        return 1
    else:
        return 0

def scrape_images():
    """
    Continuously scrape for images in the specified directory and process them.
    """
    print("Starting image scraping...")
    processed_files = set()
    
    while True:
        try:
            # producer.publish(checkTopic, calculate_distance(dangerPos, playerPos))
            print(calculate_distance(dangerPos, playerPos))
        except Exception as e:
            print(e)

        # Get a list of all .jpg and .png files in the directory
        image_files = glob.glob(os.path.join(IMAGE_DIR, "*.bmp")) + glob.glob(os.path.join(IMAGE_DIR, "*.jpg")) + glob.glob(os.path.join(IMAGE_DIR, "*.png"))
        
        for image_path in image_files:
            if image_path not in processed_files:
                print(f"Processing image: {image_path}")

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
                processed_files.add(image_path)
        
        time.sleep(SCRAPE_INTERVAL)

if __name__ == "__main__":
    scrape_images()
