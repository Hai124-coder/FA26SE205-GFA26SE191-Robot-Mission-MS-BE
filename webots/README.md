# Webots integration

`worlds/hospital_f2.wbt` contains the local `MED-01` E-puck simulation.
The controller reads `config/med_01.json`; do not put broker credentials in
this file when connecting to a shared broker.

The authoritative backend/Webots integration contract, including the future
`order` and execution-event flow, is
[`docs/mqtt-webots-contract.md`](../docs/mqtt-webots-contract.md). This README
documents only the controller capabilities currently implemented.

`maps/hospital_f2_test_map.json` and `config/hospital_f2_test_nodes.json` are
an isolated, three-node integration map for the first order-execution test.
They do not alter backend data until an Admin explicitly imports and publishes
the package. See the MQTT/Webots contract for the coordinate transform and
import sequence.

## MQTT contract used by the simulation

| Direction | Topic | QoS | Payload |
| --- | --- | --- | --- |
| Webots to backend | `rmms/v1/MED-01/state` | 1 | Robot state once per second |
| Backend to Webots | `rmms/v1/MED-01/command` | 1 | `commandId`, `command`, `issuedAt` |

The controller accepts only `STOP` and `RESUME` commands. A `STOP` immediately
stops both motors and latches the stop. `RESUME` clears that latch but does not
restart a previous movement or route; the robot remains idle until it receives
a future route or the tester changes `customData` to a new local command.

MQTT callbacks only validate and enqueue incoming commands. Motor calls execute
from the Webots simulation loop, because Webots device APIs are not safe to call
from Paho's network thread. The controller ignores duplicate `commandId` values
within its latest 100 commands.

For the local Docker Mosquitto broker, install the declared Python dependency in
the same Python runtime Webots reports in its console, then run the world:

```powershell
& "<Webots Python path>" -m pip install --user -r webots/requirements.txt
```
