# Robot Mission Management System for Autonomous Mobile Robots (RMMS)

**Capstone Project | Software Engineering | September 2026 – March 2027**

**Vietnamese title:** Hệ thống quản lý nhiệm vụ cho hệ thống robot tự hành  
**Academic supervisor:** Phan Minh Tâm, FPT University

> **Project status:** Capstone proposal / under development. Features and technologies below describe project requirements and candidate implementation choices, not necessarily completed functionality.

## Overview

The **Robot Mission Management System (RMMS)** is a proposed web-based control center for managing and coordinating a fleet of **Autonomous Mobile Robots (AMRs)**. It is intended for environments such as factories, warehouses, hospitals, airports, and logistics centers, where multiple robots carry out transport, delivery, patrol, inventory, and monitoring missions.

The platform aims to centralize mission scheduling, robot assignment, real-time fleet monitoring, alerts, activity history, and performance reporting. An **AI-assisted scheduler** supports the selection of suitable robots and the prioritization of missions.

**Scope:** The project focuses on **fleet coordination and mission-management software**, not designing or building physical robots. A robot simulation environment will be used for integration and testing.

## Project Objectives

- Centrally manage robots, their capabilities, availability, and operational states.
- Create, schedule, assign, and track missions across the fleet.
- Match missions to robots based on **availability, battery level, and capabilities**.
- Monitor robot telemetry and mission progress in real time.
- Generate notifications for abnormal events and mission outcomes.
- Analyze fleet performance through reports and dashboards.
- Apply AI/optimization techniques to mission assignment and priority prediction.

## User Roles

The proposal identifies three system roles:

| Role | Description |
| --- | --- |
| **Administrator** | System user and role management. |
| **Operator** | Fleet monitoring and mission coordination. |
| **Supervisor** | Identified as a system actor; exact permissions and responsibilities are to be defined in the detailed requirements. |

The role descriptions above are high-level. A complete access-control matrix has not yet been defined in the source proposal.

## Core Functional Modules

### 1. User Management
- Authentication (login/logout).
- User and role management.
- Activity logging.

### 2. Robot Fleet Management
- Register and manage robots.
- Maintain robot identity, type, capabilities, and current state.

### 3. Mission Management (Core Module)
- Create, assign, schedule, and track missions.
- Support mission types such as goods transport, medicine delivery, patrol, inventory, and environmental monitoring.
- Assign suitable robots using availability, battery level, and capabilities.
- Record mission status and execution history.

### 4. Real-Time Fleet Monitoring
- Display robot position and operational status.
- Track battery level, speed, current mission progress, and last heartbeat.
- Surface alerts through the control-center dashboard.

### 5. Notifications & Alerts
- Robot offline.
- Low battery.
- Mission timeout.
- Robot error.
- Mission completion.
- Collision warning.

### 6. Reporting & Analytics
- Robot utilization.
- Mission success rate and duration.
- Battery usage.
- Robot downtime.
- Average response time.

### 7. AI Scheduler & Mission Priority Prediction
- Assist robot-to-mission assignment.
- Predict or recommend mission priorities, such as prioritizing medicine delivery over routine inspection when appropriate.
- Evaluate scheduling/optimization approaches as part of the capstone work.

## Proposed System Architecture

The project is expected to integrate the following logical components:

```text
                  +---------------------------+
                  |  Web Control Center       |
                  |  Dashboard / Management   |
                  +-------------+-------------+
                                |
                       Backend APIs
                                |
                  +-------------v-------------+
                  |  Mission & Fleet Platform |
                  |  Users / Missions / Logs  |
                  +---+----------+------------+
                      |          |
              +-------v---+  +---v----------------+
              | Database  |  | AI Scheduler       |
              |           |  | Assignment/Priority|
              +-----------+  +--------------------+
                      |
            Telemetry / Integration Layer
                      |
              +-------v--------------------+
              | Robot Simulator + Bridge  |
              | Simulated AMR Fleet       |
              +----------------------------+
```

*This is a conceptual illustration of the proposed components, not a finalized deployment architecture.* Real-time robot state is expected to flow through a bridge/API using a suitable communication mechanism.

## Candidate Technology Stack

The registration document lists **alternatives** rather than a finalized stack:

| Layer | Candidate technologies |
| --- | --- |
| Web frontend | HTML5, CSS3, JavaScript, React or Angular |
| Backend | .NET Core, Spring Boot, or Node.js |
| Database | SQL Server, MySQL, or PostgreSQL |
| Real-time communication | WebSocket or MQTT |
| Robot simulation/integration | Gazebo, Webots, CoppeliaSim, ROS 2; bridge/API integration |
| AI scheduler service | Python with FastAPI or Flask; optimization / machine learning |
| System modeling | UML 2.0 |

**Final technology choices and repository-specific setup instructions will be documented once confirmed.**

## Non-Functional Requirements

- **Usability:** Provide an intuitive interface for operators to monitor and coordinate the fleet.
- **Reliability:** Execute the specified functions consistently.
- **Scalability:** Support growth in the number of robots and missions.

## Expected Deliverables

- Web-based control center for administrators, operators, and supervisors.
- Real-time fleet-monitoring dashboard connected to a robot simulator.
- AI-assisted scheduling service for mission assignment and priority prediction.
- Backend APIs and deployable software packages.
- Source code and supporting technical documentation.

### Documentation

- User Requirements
- Software Requirements Specification (SRS)
- Architecture Design and Detailed Design
- System Implementation documentation
- Testing documentation
- Installation Guide and User Manual
- AI Scheduler Design and Evaluation Report

## Project Work Packages

1. Set up the robot simulator and telemetry bridge/API.
2. Implement backend APIs for users, robots, missions, and real-time telemetry.
3. Develop and evaluate the AI scheduler for mission assignment and priority prediction.
4. Build the web control center, dashboard, alerts, and reporting features.
5. Integrate, deploy, and test the system.
6. Prepare system analysis, design, testing, installation, and user documentation.

## Implementation Notes

This README summarizes the **official capstone registration proposal**. It does **not** imply that all functions have been implemented. The following details require separate design decisions and should be documented as development progresses:

- Exact permissions for each role, especially Supervisor.
- Detailed mission workflows, state transitions, and exception handling.
- AI scheduling features, training/evaluation methodology, and priority scoring.
- Selected simulator, technology stack, and deployment architecture.
- Environment configuration, installation steps, and API documentation.

## Academic Context

This is a Software Engineering capstone project at **FPT University**, scheduled for **September 2026 to March 2027**, under the supervision of **Phan Minh Tâm**.

---

*Based on the project registration document “Robot Mission Management System for Autonomous Mobile Robots” (dated August 19, 2026).*
