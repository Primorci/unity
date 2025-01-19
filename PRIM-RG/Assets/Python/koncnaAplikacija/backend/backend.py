import os
import subprocess
import time
import glob
import torch
import cv2
import numpy as np
from PIL import Image
import paho.mqtt.client as mqtt

from dekompresija_slik_unity import decompress, save_bmp_from_pixels, binary_file_to_bits

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
topic = "/YOLO/result"

def on_connect(client, userdata, flags, reasonCode, properties=None):
    print("Connected to MQTT Broker successfully.")


def on_disconnect(client, userdata, rc):
    print(f"Disconnected from MQTT Broker. Reason: {rc}")

producer = mqtt.Client(client_id="producer_1", callback_api_version=mqtt.CallbackAPIVersion.VERSION2)

# Connect to MQTT broker
try:
    # Setup MQTT client
    producer.on_connect = on_connect
    producer.on_disconnect = on_disconnect

    producer.connect(broker, port, 60)
    producer.loop_forever()  # Start a new thread to handle network traffic and dispatching callbacks
except:
    print(f"Error: Can not connect to MQTT on port {port}, addr {broker}");

import cv2
import json

def process_image(image_path):
    """
    Process a single image using the YOLO models and return the detected objects in JSON format.
    """
    # Load and preprocess the image
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
    detected_road = []
    
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

    
    print(type(largest_road_type))

    # Format results as a dictionary
    result = {
        "detected_danger": True if len(detected_danger) > 0 else False,
        "danger_type": list(set(detected_danger)),
        "road_type": largest_road_type
    }
    
    # Convert the dictionary to a JSON string
    return json.dumps(result, indent=4)

def scrape_images():
    """
    Continuously scrape for images in the specified directory and process them.
    """
    print("Starting image scraping...")
    processed_files = set()

    while True:
        # Get a list of all .jpg and .png files in the directory
        image_files = glob.glob(os.path.join(IMAGE_DIR, "*.bmp")) + glob.glob(os.path.join(IMAGE_DIR, "*.jpg")) + glob.glob(os.path.join(IMAGE_DIR, "*.png"))
        bin_files = glob.glob(os.path.join("E:/Primorci/unity/PRIM-RG/Assets/Screenshots/Compress/", "*.bin"))
        
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
