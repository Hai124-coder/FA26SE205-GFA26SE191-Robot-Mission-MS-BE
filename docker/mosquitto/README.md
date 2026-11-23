# MQTT broker cho môi trường phát triển

Chạy từ thư mục gốc backend:

```powershell
docker compose up -d
docker compose ps
```

Broker local nghe tại `localhost:1883`, không dùng TLS và chỉ dành cho máy phát triển.
Xem log: `docker compose logs mqtt`. Dừng broker: `docker compose down`.

Kiểm thử nhận state bằng một terminal:

```powershell
docker exec -it rmms-mqtt mosquitto_sub -h localhost -t "rmms/v1/+/state" -v
```

Sau đó dùng terminal khác để publish một state mock:

```powershell
docker exec -it rmms-mqtt mosquitto_pub -h localhost -t "rmms/v1/MED-01/state" -m '{"robotId":"MED-01","ts":"2026-11-23T10:00:00Z","x":2.5,"y":1.2,"theta":0,"battery":92,"speed":0.4,"status":"MOVING","orderId":null,"nextNodeId":"F2-N02","atStopSeq":1}'
```
