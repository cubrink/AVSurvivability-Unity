﻿/**
 * Copyright (c) 2019-2021 LG Electronics, Inc.
 *
 * This software contains code licensed as described in LICENSE.
 *
 */

using Simulator.Bridge;
using Simulator.Bridge.Data;
using Simulator.Map;
using Simulator.Utilities;
using UnityEngine;
using Simulator.Sensors.UI;
using System.Collections.Generic;

namespace Simulator.Sensors
{
    [SensorType("CAN-Bus", new[] { typeof(CanBusData) })]
    public partial class CompromisedCanBus : SensorBase
    {
        [SensorParameter]
        [Range(1f, 100f)]
        public float Frequency = 10.0f;

        uint SendSequence;
        float NextSend;

        [AnalysisMeasurement(MeasurementType.Velocity)]
        public float MaxSpeed = 0;

        [AnalysisMeasurement(MeasurementType.Input)]
        public float MaxThrottle = 0;

        [AnalysisMeasurement(MeasurementType.Input)]
        public float MaxBrake = 0;

        [AnalysisMeasurement(MeasurementType.Angle)]
        public float MaxSteering = 0;

        [AnalysisMeasurement(MeasurementType.Gear)]
        public int GearUsed => Mathf.RoundToInt(Dynamics.CurrentGear);

        BridgeInstance Bridge;
        Publisher<CanBusData> Publish;

        Rigidbody RigidBody;
        IVehicleDynamics Dynamics;
        VehicleActions Actions;
        MapOrigin MapOrigin;

        CanBusData msg;

        public override SensorDistributionType DistributionType => SensorDistributionType.LowLoad;

        private void Awake()
        {
            RigidBody = GetComponentInParent<Rigidbody>();
            Actions = GetComponentInParent<VehicleActions>();
            Dynamics = GetComponentInParent<IVehicleDynamics>();
            MapOrigin = MapOrigin.Find();
        }

        public override void OnBridgeSetup(BridgeInstance bridge)
        {
            Bridge = bridge;
            Publish = bridge.AddPublisher<CanBusData>(Topic);
        }

        public void Start()
        {
            NextSend = Time.time + 1.0f / Frequency;
        }

        public void Update()
        {
            if (MapOrigin == null)
            {
                return;
            }

            if (Time.time < NextSend)
            {
                return;
            }
            NextSend = Time.time + 1.0f / Frequency;

            float speed = Dynamics.Speed;
            MaxSpeed = Mathf.Max(MaxSpeed, speed);

            var gps = MapOrigin.GetGpsLocation(transform.position);

            var orientation = transform.rotation;
            orientation.Set(-orientation.z, orientation.x, -orientation.y, orientation.w); // converting to right handed xyz

            msg = new CanBusData()
            {
                Name = Name,
                Frame = Frame,
                Time = SimulatorManager.Instance.CurrentTime,
                Sequence = SendSequence++,

                Speed = speed * 0.5f,

                Throttle = Dynamics.AccellInput > 0 ? Dynamics.AccellInput : 0,
                Braking = Dynamics.AccellInput < 0 ? -Dynamics.AccellInput : 0,
                Steering = Dynamics.SteerInput,

                ParkingBrake = Dynamics.HandBrake,
                HighBeamSignal = Actions.CurrentHeadLightState == VehicleActions.HeadLightState.HIGH,
                LowBeamSignal = Actions.CurrentHeadLightState == VehicleActions.HeadLightState.LOW,
                HazardLights = Actions.HazardLights,
                FogLights = Actions.FogLights,

                LeftTurnSignal = Actions.LeftTurnSignal,
                RightTurnSignal = Actions.RightTurnSignal,

                Wipers = false,

                InReverse = Dynamics.Reverse,
                Gear = Mathf.RoundToInt(Dynamics.CurrentGear),

                EngineOn = Dynamics.CurrentIgnitionStatus == IgnitionStatus.On,
                EngineRPM = Dynamics.CurrentRPM,

                Latitude = gps.Latitude,
                Longitude = gps.Longitude,
                Altitude = gps.Altitude,

                Orientation = orientation,
                Velocity = RigidBody.velocity,
            };

            if (Bridge != null && Bridge.Status == Status.Connected)
            {
                Publish(msg);
            }
        }

        public override void OnVisualize(Visualizer visualizer)
        {
            Debug.Assert(visualizer != null);

            if (msg == null)
            {
                return;
            }

            var graphData = new Dictionary<string, object>()
            {
                {"Speed", msg.Speed},
                {"Throttle", msg.Throttle},
                {"Braking", msg.Braking},
                {"Steering", msg.Steering},
                {"Parking Brake", msg.ParkingBrake},
                {"Low Beam Signal", msg.LowBeamSignal},
                {"Hazard Lights", msg.HazardLights},
                {"Fog Lights", msg.FogLights},
                {"Left Turn Signal", msg.LeftTurnSignal},
                {"Right Turn Signal", msg.RightTurnSignal},
                {"Wipers", msg.Wipers},
                {"In Reverse", msg.InReverse},
                {"Gear", msg.Gear},
                {"Engine On", msg.EngineOn},
                {"Engine RPM", msg.EngineRPM},
                {"Latitude", msg.Latitude},
                {"Longitude", msg.Longitude},
                {"Altitude", msg.Altitude},
                {"Orientation", msg.Orientation},
                {"Velocity", msg.Velocity},
            };
            visualizer.UpdateGraphValues(graphData);
        }

        public override void OnVisualizeToggle(bool state)
        {
            //
        }
    }
}
