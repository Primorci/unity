import paho.mqtt.client as mqtt
from prometheus_client import start_http_server, Gauge, Counter, Histogram

SCRAPE_INTERVAL = 0.2

# Car metrics --------------------------------------
car_speed = Gauge('player_speed', 'Speed of the player\'s car')
distance_traveled = Gauge('player_distance_traveled', 'Distance traveled with the car')
car_state = Gauge('player_car_state', 'Current state of the car (1 = idle, 2 = braking, 3 = accelerating, 4 = steering)')

# Key press counter
key_press_counter = Counter('player_key_presses_total', 'Total number of key presses, categorized by key', ['key'])

# Performance metrics --------------------------------------
fps = Gauge('performance_fps', 'Current frames per second')

# Session metrics --------------------------------------
session_duration = Histogram(
    'session_duration_seconds', 
    'Duration of user sessions in seconds',
    buckets=[30, 60, 120, 240, 480, 960, 1920, 3840, 7680, 15360, float('inf')]
)

# Road metrics --------------------------------------
road_generation_time = Histogram(
    'road_generation_time_seconds', 
    'Time taken to generate a road segment in seconds', 
    buckets=[0.1 * i for i in range(1, 21)]
)

road_unload_time = Histogram(
    'road_unload_time_seconds', 
    'Time taken to unload a road segment in seconds', 
    buckets=[0.1 * i for i in range(1, 21)]
)

# Road type counter
road_type = Counter('road_type_total', 'Total number of roads categorized by road type', ['road_type'])

# Exit point count histogram
exit_point_count = Histogram(
    'road_exit_point_count', 
    'Distribution of exit point counts for road events', 
    buckets=[i for i in range(1, 11)]
)

# Road position gauges
road_position_x = Gauge('road_position_x', 'X position of the road')
road_position_y = Gauge('road_position_y', 'Y position of the road')
road_position_z = Gauge('road_position_z', 'Z position of the road')

# Obstacle metrics --------------------------------------
obstacle_generation_time = Histogram(
    'obstacle_generation_time_seconds', 
    'Time taken to generate obstacle on the road in seconds', 
    buckets=[0.1 * i for i in range(1, 21)]
)

obstacle_destruction_time = Histogram(
    'obstacle_destruction_time_seconds', 
    'Time taken to destroy obstacle on the road in seconds', 
    buckets=[0.1 * i for i in range(1, 21)]
)

# Obstacle type counter
obstacle_type = Counter('obstacle_type_total', 'Total number of obstacles categorized by obstacle type', ['obstacle_type'])

# Obstacle position gauges
obstacle_position_x = Gauge('obstacle_position_x', 'X position of the obstacle')
obstacle_position_y = Gauge('obstacle_position_y', 'Y position of the obstacle')
obstacle_position_z = Gauge('obstacle_position_z', 'Z position of the obstacle')

# Health metrics --------------------------------------
error_count = Counter('health_error_count', 'Total number of errors in a session', ['type'])
warning_count = Counter('health_warning_count', 'Total number of warnings in a session', ['type'])

start_http_server(5555)

broker = "10.8.1.6"
port = 1883
topic = "/Prometheus/unity"

def on_connect(client, userdata, flags, reason_code, properties):
    if reason_code.is_failure:
        print(f"Failed to connect: {reason_code}. loop_forever() will retry connection")
    else:
        print("Connected to MQTT Broker successfully.")
        topics = [
            ("game/player", 0), 
            ("game/performance/fps", 0), 
            ("game/road/generation", 0), 
            ("ggame/road/generation/error", 0),
            ("game/road/degeneration", 0),
            ("game/road/danger", 0),
            ("/YOLO/result", 0)
        ]
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
    topic = msg.topic
    payload = json.loads(msg.payload.decode())

    if topic == "game/player":
        # Handle "game/player" topic
        car_speed
    elif topic == "game/performance/fps":
        # Handle "game/performance/fps" topic
        pass
    elif topic == "game/road/generation":
        # Handle "game/road/generation" topic
        pass
    elif topic == "game/road/generation/error":
        # Handle "game/road/generation/error" topic
        pass
    elif topic == "game/road/degeneration":
        # Handle "game/road/degeneration" topic
        pass
    elif topic == "game/road/danger":
        # Handle "game/road/danger" topic
        pass
    elif topic == "/YOLO/result":
        # Handle "/YOLO/result" topic
        pass
    break  # Once a match is found, exit the loop

producer = mqtt.Client(client_id="backendProducer", callback_api_version=mqtt.CallbackAPIVersion.VERSION2)

# Connect to MQTT broker
try:
    # Setup MQTT client
    producer.on_connect = on_connect
    producer.on_message = on_message
    producer.on_subscribe = on_subscribe
    producer.on_unsubscribe = on_unsubscribe

    producer.connect(broker, port, 60)
    producer.loop_forever()
except:
    print(f"Error: Can not connect to MQTT on port {port}, addr {broker}");
