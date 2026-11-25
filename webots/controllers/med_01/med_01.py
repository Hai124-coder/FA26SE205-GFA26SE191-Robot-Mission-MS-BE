"""Safe local-command controller for the MED-01 Webots robot.

The command comes from the E-puck's ``customData`` field so it can be tested
inside Webots before the controller is connected to MQTT. Supported values:

* ``STOP``
* ``MOVE_FORWARD``
* ``{\"command\": \"MOVE_FORWARD\", \"durationSeconds\": 2}``
"""

import json
import math
import sys
from datetime import datetime, timezone
from pathlib import Path

from controller import Robot
import paho.mqtt.client as mqtt


CONFIG_PATH = Path(__file__).resolve().parents[2] / "config" / "med_01.json"


def load_config() -> dict:
    """Load and validate non-secret local simulation settings."""
    try:
        config = json.loads(CONFIG_PATH.read_text(encoding="utf-8"))
    except FileNotFoundError as error:
        raise RuntimeError(f"Configuration file was not found: {CONFIG_PATH}") from error
    except json.JSONDecodeError as error:
        raise RuntimeError(f"Configuration file contains invalid JSON: {CONFIG_PATH}") from error

    mqtt_config = config.get("mqtt")
    simulation_config = config.get("simulation")
    if not isinstance(config.get("robotId"), str) or not config["robotId"].strip():
        raise RuntimeError("Configuration requires a non-empty robotId.")
    if not isinstance(mqtt_config, dict) or not isinstance(simulation_config, dict):
        raise RuntimeError("Configuration requires mqtt and simulation sections.")
    if not isinstance(mqtt_config.get("host"), str) or not mqtt_config["host"].strip():
        raise RuntimeError("Configuration requires mqtt.host.")
    if not isinstance(mqtt_config.get("port"), int) or not 1 <= mqtt_config["port"] <= 65535:
        raise RuntimeError("Configuration requires mqtt.port between 1 and 65535.")
    if not isinstance(mqtt_config.get("stateTopic"), str) or "{robotId}" not in mqtt_config["stateTopic"]:
        raise RuntimeError("Configuration requires mqtt.stateTopic containing {robotId}.")

    return config


config = load_config()
mqtt_config = config["mqtt"]
simulation_config = config["simulation"]

ROBOT_ID = config["robotId"].strip()
MQTT_HOST = mqtt_config["host"].strip()
MQTT_PORT = mqtt_config["port"]
MQTT_TOPIC = mqtt_config["stateTopic"].format(robotId=ROBOT_ID)
TELEMETRY_INTERVAL_SECONDS = float(mqtt_config.get("telemetryIntervalSeconds", 1.0))
DEFAULT_COMMAND_DURATION_SECONDS = float(simulation_config.get("defaultCommandDurationSeconds", 2.0))
MAX_COMMAND_DURATION_SECONDS = float(simulation_config.get("maxCommandDurationSeconds", 10.0))
FORWARD_WHEEL_SPEED = float(simulation_config.get("forwardWheelSpeed", 2.0))
SIMULATED_BATTERY_PERCENT = int(simulation_config.get("batteryPercent", 100))

if TELEMETRY_INTERVAL_SECONDS <= 0:
    raise RuntimeError("mqtt.telemetryIntervalSeconds must be greater than zero.")
if not 0 <= SIMULATED_BATTERY_PERCENT <= 100:
    raise RuntimeError("simulation.batteryPercent must be between 0 and 100.")
if not 0 < DEFAULT_COMMAND_DURATION_SECONDS <= MAX_COMMAND_DURATION_SECONDS:
    raise RuntimeError("Simulation command durations are invalid.")


robot = Robot()
timestep = int(robot.getBasicTimeStep())

left_motor = robot.getDevice("left wheel motor")
right_motor = robot.getDevice("right wheel motor")
gps = robot.getDevice("gps")
compass = robot.getDevice("compass")

for motor in (left_motor, right_motor):
    motor.setPosition(float("inf"))
    motor.setVelocity(0.0)

gps.enable(timestep)
compass.enable(timestep)


def stop() -> None:
    """Stop both wheels immediately."""
    left_motor.setVelocity(0.0)
    right_motor.setVelocity(0.0)


def move_forward() -> None:
    """Drive both wheels in the forward direction."""
    left_motor.setVelocity(FORWARD_WHEEL_SPEED)
    right_motor.setVelocity(FORWARD_WHEEL_SPEED)


def current_pose() -> tuple[float, float, float]:
    """Return backend map coordinates (x, y) and heading in radians.

    Webots uses X/Z as the floor plane. The API contract calls its second map
    coordinate ``y``, so the simulation Z coordinate is sent as backend Y.
    """
    position = gps.getValues()
    north = compass.getValues()
    heading_rad = math.atan2(north[0], north[2])
    return position[0], position[2], heading_rad


def on_mqtt_connect(client, userdata, connect_flags, reason_code, properties) -> None:
    """Report connection state without stopping the simulation on failures."""
    if reason_code == 0:
        print(f"MED-01 MQTT connected to {MQTT_HOST}:{MQTT_PORT}.")
    else:
        print(f"MED-01 MQTT connection refused: {reason_code}.")


def on_mqtt_disconnect(client, userdata, disconnect_flags, reason_code, properties) -> None:
    if reason_code != 0:
        print("MED-01 MQTT disconnected; reconnecting in the background.")


def create_mqtt_client() -> mqtt.Client:
    """Create a reconnecting MQTT client. Network failures do not stop Webots."""
    client = mqtt.Client(
        callback_api_version=mqtt.CallbackAPIVersion.VERSION2,
        client_id=f"webots-{ROBOT_ID}",
        protocol=mqtt.MQTTv311,
    )
    client.on_connect = on_mqtt_connect
    client.on_disconnect = on_mqtt_disconnect
    client.reconnect_delay_set(min_delay=1, max_delay=5)
    client.connect_async(MQTT_HOST, MQTT_PORT, keepalive=20)
    client.loop_start()
    return client


def publish_state(client: mqtt.Client, x: float, y: float, theta: float, speed: float, status: str) -> None:
    """Publish the robot-state contract expected by RobotStateSubscriberWorker."""
    if not client.is_connected():
        return

    payload = {
        "robotId": ROBOT_ID,
        "ts": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "x": round(x, 4),
        "y": round(y, 4),
        "theta": round(theta, 4),
        "battery": SIMULATED_BATTERY_PERCENT,
        "speed": round(speed, 4),
        "status": status,
        "orderId": None,
        "nextNodeId": None,
        "atStopSeq": None,
    }
    result = client.publish(MQTT_TOPIC, json.dumps(payload), qos=1)
    if result.rc != mqtt.MQTT_ERR_SUCCESS:
        print(f"MED-01 MQTT publish failed with code {result.rc}.")


def parse_command(raw_command: str) -> tuple[str, float]:
    """Read either a simple command word or a small JSON command object."""
    normalized = raw_command.strip()
    if not normalized:
        return "STOP", 0.0

    if normalized in {"STOP", "MOVE_FORWARD"}:
        return normalized, DEFAULT_COMMAND_DURATION_SECONDS

    try:
        payload = json.loads(normalized)
    except json.JSONDecodeError as error:
        raise ValueError("customData must be STOP, MOVE_FORWARD, or valid JSON") from error

    if not isinstance(payload, dict) or not isinstance(payload.get("command"), str):
        raise ValueError("JSON customData requires a string 'command'")

    command = payload["command"].strip()
    duration = payload.get("durationSeconds", DEFAULT_COMMAND_DURATION_SECONDS)
    if not isinstance(duration, (int, float)) or isinstance(duration, bool):
        raise ValueError("durationSeconds must be a number")

    return command, max(0.1, min(float(duration), MAX_COMMAND_DURATION_SECONDS))


active_until = None
last_custom_data = None
next_pose_log_at = 0.0
last_telemetry_pose = None
mqtt_client = create_mqtt_client()
print(f"MED-01 controller ready for local commands. Robot: {robot.getName()}")
print(f"MED-01 Webots Python: {sys.executable}")
print(f"MED-01 configuration: {CONFIG_PATH}")

while robot.step(timestep) != -1:
    custom_data = robot.getCustomData()

    if custom_data != last_custom_data:
        last_custom_data = custom_data
        try:
            command, duration_seconds = parse_command(custom_data)
        except ValueError as error:
            stop()
            active_until = None
            print(f"MED-01 ignored invalid customData: {error}")
            continue

        if command == "STOP":
            stop()
            active_until = None
            print("MED-01 stopped by local command.")
        elif command == "MOVE_FORWARD":
            move_forward()
            active_until = robot.getTime() + duration_seconds
            print(f"MED-01 moving forward for {duration_seconds:g} seconds.")
        else:
            stop()
            active_until = None
            print(f"MED-01 ignored unsupported command: {command}")

    if active_until is not None and robot.getTime() >= active_until:
        stop()
        active_until = None
        print("MED-01 command duration reached; robot stopped safely.")

    if robot.getTime() >= next_pose_log_at:
        x, y, heading_rad = current_pose()
        if last_telemetry_pose is None:
            speed = 0.0
        else:
            previous_x, previous_y, previous_time = last_telemetry_pose
            elapsed = max(robot.getTime() - previous_time, 0.001)
            speed = math.hypot(x - previous_x, y - previous_y) / elapsed

        status = "MOVING" if active_until is not None else "IDLE"
        publish_state(mqtt_client, x, y, heading_rad, speed, status)
        print(f"MED-01 state: {status} x={x:.3f}, y={y:.3f}, theta={heading_rad:.3f}, speed={speed:.3f}")
        last_telemetry_pose = (x, y, robot.getTime())
        next_pose_log_at = robot.getTime() + TELEMETRY_INTERVAL_SECONDS

mqtt_client.loop_stop()
mqtt_client.disconnect()
