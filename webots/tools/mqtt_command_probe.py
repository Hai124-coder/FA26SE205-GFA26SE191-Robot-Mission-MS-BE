"""Print one MQTT command message for local integration testing."""

import json
import sys
import threading

import paho.mqtt.client as mqtt


if len(sys.argv) != 2:
    raise SystemExit("usage: mqtt_command_probe.py <robot-code>")

robot_code = sys.argv[1]
topic = f"rmms/v1/{robot_code}/command"
received = threading.Event()


def on_connect(client, userdata, connect_flags, reason_code, properties):
    if reason_code != 0:
        raise RuntimeError(f"MQTT connection refused: {reason_code}")
    client.subscribe(topic, qos=1)


def on_message(client, userdata, message):
    payload = json.loads(message.payload.decode("utf-8"))
    print(json.dumps({"topic": message.topic, "payload": payload}), flush=True)
    received.set()


client = mqtt.Client(callback_api_version=mqtt.CallbackAPIVersion.VERSION2, client_id="rmms-command-probe")
client.on_connect = on_connect
client.on_message = on_message
client.connect("localhost", 1883, keepalive=20)
client.loop_start()
if not received.wait(20):
    client.loop_stop()
    client.disconnect()
    raise SystemExit("No MQTT command received within 20 seconds.")
client.loop_stop()
client.disconnect()
