import os
import cv2
import tkinter as tk
from tkinter import filedialog, Label, Button
from PIL import Image, ImageTk
import threading
import torch
import numpy as np

# Load YOLO models
model_danger = torch.hub.load('ultralytics/yolov5', 'custom', path='Yolo\DangerBest.pt')
model_road = torch.hub.load('ultralytics/yolov5', 'custom', path='Yolo\RoadBest.pt')

def save_frame(output_folder, frame_rgb, image_count):
    """Save frame to PNG asynchronously."""
    pil_image = Image.fromarray(frame_rgb)
    output_path = os.path.join(output_folder, f"frame_{image_count:04d}.png")
    pil_image.save(output_path)

def process_yolo(frame, model, show_results=True):
    results = model(frame)
    if show_results:
        results.render()  # Render boxes on the frame
    return results

def resize_image(image, max_size=(800, 600)):
    h, w = image.shape[:2]
    ratio = min(max_size[0] / w, max_size[1] / h)
    new_size = (int(w * ratio), int(h * ratio))
    resized_image = cv2.resize(image, new_size, interpolation=cv2.INTER_LANCZOS4)
    return resized_image

def convert_mp4_to_png_with_yolo(mp4_path, output_folder, interval=0.5):
    """Convert MP4 to PNG with YOLO detection and frame saving."""
    cap = cv2.VideoCapture(mp4_path)
    fps = cap.get(cv2.CAP_PROP_FPS)
    frame_rate = int(fps * interval)

    if not cap.isOpened():
        print("Error: Could not open video.")
        return

    os.makedirs(output_folder, exist_ok=True)

    frame_count = 0
    image_count = 0

    # Create a named window
    cv2.namedWindow("Video Playback with YOLO", cv2.WINDOW_NORMAL)

    while True:
        ret, frame = cap.read()
        if not ret:
            break

        # Apply YOLO models
        frame = cv2.resize(frame, (800, 600))
        frameToPNG = frame
        
        frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        frameToPNG = cv2.cvtColor(frameToPNG, cv2.COLOR_BGR2RGB)

        results_danger = process_yolo(frame, model_danger)
        results_road = process_yolo(frame, model_road)

        # Display the video frame
        cv2.imshow("Video Playback with YOLO", cv2.cvtColor(frame, cv2.COLOR_RGB2BGR))

        # Save frames as PNG every interval using a separate thread
        if frame_count % frame_rate == 0:
            thread = threading.Thread(target=save_frame, args=(output_folder, frameToPNG, image_count))
            thread.start()
            image_count += 1

        frame_count += 1

        # Exit video playback on 'q' key press
        if cv2.waitKey(1) & 0xFF == ord('q'):
            break

    cap.release()
    cv2.destroyAllWindows()
    print(f"Frames saved in: {output_folder}")

def upload_mp4_with_yolo():
    """Upload an MP4 file and process it with YOLO."""
    file_path = filedialog.askopenfilename(title="Select MP4 File", filetypes=[("MP4 files", "*.mp4")])
    if file_path:
        label.config(text=f"Selected file: {file_path}")
        output_folder = "imgs"
        convert_mp4_to_png_with_yolo(file_path, output_folder)
        label.config(text=f"Frames saved in: {output_folder}")

# Tkinter GUI
root = tk.Tk()
root.title("MP4 to PNG Converter with YOLO")
root.geometry("400x200")
root.minsize(400, 200)

label = Label(root, text="Select an MP4 file to convert with YOLO", wraplength=350)
label.pack(pady=20)

button = Button(root, text="Upload MP4", command=upload_mp4_with_yolo)
button.pack(pady=10)

root.mainloop()
