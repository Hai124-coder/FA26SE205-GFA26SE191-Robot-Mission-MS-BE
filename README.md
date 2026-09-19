# \# Robot Mission Management System for Autonomous Mobile Robots

# 

# A web-based platform for managing and coordinating a fleet of Autonomous Mobile Robots (AMRs).

# 

# The system provides centralized robot fleet management, mission scheduling and assignment,

# real-time monitoring, notifications, reporting, and AI-assisted mission scheduling.

# 

# This project is developed as a Capstone Project at FPT University.

# 

# \---

# 

# \## Overview

# 

# In environments such as smart factories, warehouses, hospitals, airports, and logistics centers,

# multiple autonomous mobile robots may operate at the same time.

# 

# Managing a large robot fleet introduces several challenges:

# 

# \- Which robot should perform a mission?

# \- Which robots are currently available?

# \- Which robots have enough battery?

# \- Which robots have the required capabilities?

# \- Where are the robots currently located?

# \- Which missions should be prioritized?

# 

# The Robot Mission Management System acts as a centralized control center to manage

# robots, missions, telemetry, and scheduling.

# 

# Physical robots are not required. Robot behavior is simulated using a robot simulator

# and integrated with the platform through APIs or real-time communication.

# 

# \---

# 

# \## Main Features

# 

# \### User Management

# 

# \- Login and logout

# \- User management

# \- Role-based access control

# \- Activity logging

# 

# Supported roles:

# 

# \- Administrator

# \- Operator

# \- Supervisor

# 

# \### Robot Fleet Management

# 

# \- Register robots

# \- Manage robot information

# \- Manage robot type and capabilities

# \- Track robot status

# \- Track battery level

# \- Track current location

# \- Track robot heartbeat

# 

# \### Mission Management

# 

# \- Create missions

# \- Assign missions

# \- Schedule missions

# \- Track mission progress

# \- Store mission history

# \- Automatically match robots with missions

# 

# Robot selection may be based on:

# 

# \- Availability

# \- Battery level

# \- Robot capabilities

# \- Mission requirements

# 

# \### Real-Time Fleet Monitoring

# 

# The dashboard displays:

# 

# \- Robot location

# \- Robot status

# \- Battery level

# \- Current speed

# \- Mission progress

# \- Last heartbeat

# \- Alerts and warnings

# 

# \### Notifications

# 

# The system can generate alerts for:

# 

# \- Robot offline

# \- Low battery

# \- Mission timeout

# \- Robot error

# \- Mission completed

# \- Collision warning

# 

# \### Reporting

# 

# The system provides reports for:

# 

# \- Robot utilization

# \- Mission success rate

# \- Mission duration

# \- Battery usage

# \- Robot downtime

# \- Average mission response time

# 

# \### AI Scheduler

# 

# The AI scheduler assists with:

# 

# \- Robot-to-mission assignment

# \- Mission priority prediction

# 

# For example, an urgent medicine delivery mission may receive a higher priority

# than a routine warehouse inspection mission.

# 

# \---

# 

# \## System Architecture

# 

# The system consists of several main components:

# 

# ```text

# Robot Simulator

# &#x20;     |

# &#x20;     | Telemetry / Robot State

# &#x20;     v

# Robot Bridge / API

# &#x20;     |

# &#x20;     v

# Backend API

# &#x20;     |

# &#x20;     +-------------------+

# &#x20;     |                   |

# &#x20;     v                   v

# Database             AI Scheduler

# &#x20;     |

# &#x20;     v

# Web Control Center

