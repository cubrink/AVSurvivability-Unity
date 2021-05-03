using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Simulator.Bridge;
using Simulator.Bridge.Data;
using Simulator.Map;
using Simulator.Utilities;
using Simulator.Sensors.UI;

namespace Simulator.Sensors
{
    [SensorType("Vehicle Sensor", new[] {typeof(CanBusData)})]
    public class VehicleSensor : SensorBase
    {

        [SensorParameter]
        [Range(1.0f, 100f)]
        public float Frequency = 10.0f;


        [AnalysisMeasurement(MeasurementType.Count)]
        public int MaxTracked = -1;
        public float MaxDistance = 100.0f;

        public RangeTrigger RangeTrigger;

        private Dictionary<uint, Tuple<Detected3DObject, Collider>> Detected;
        private HashSet<uint> CurrentIDs;

        public override SensorDistributionType DistributionType => SensorDistributionType.HighLoad;

        public override void OnBridgeSetup(BridgeInstance bridge)
        {
            // Ignore
            return;
        }


        // Start is called before the first frame update
        void Start()
        {
            if (RangeTrigger == null)
            {
                RangeTrigger = GetComponentInChildren<RangeTrigger>();
            }

            RangeTrigger.SetCallbacks(WhileInRange); // Callback for objects in range
            RangeTrigger.transform.localScale = MaxDistance * Vector3.one;  // Set range as MaxDistance * <1, 1, 1>

            Detected = new Dictionary<uint, Tuple<Detected3DObject, Collider>>();
            CurrentIDs = new HashSet<uint>();
        }


        /*
         * FixedUpdate()
         *
         * Called at a specified rate, consistent with real time, not game time
         */
        private void FixedUpdate()
        {
            MaxTracked = Math.Max(MaxTracked, CurrentIDs.Count);
            CurrentIDs.Clear();
        }

        public override void OnVisualize(Visualizer visualizer)
        {
            Debug.Assert(visualizer != null);

            var graphData = new Dictionary<string, object>()
            {
                {"Measurement Span", Time.fixedDeltaTime},
                {"Total vehicles", CurrentIDs.Count}
            };
            visualizer.UpdateGraphValues(graphData);
        }

        public override void OnVisualizeToggle(bool state) {}


         void WhileInRange(Collider other)
        {
            GameObject egoGO = transform.parent.gameObject; // Ego vehicle
            GameObject parent = other.transform.parent.gameObject;
            if (parent == egoGO)
            {
                return;
            }

            if (!(other.gameObject.layer == LayerMask.NameToLayer("GroundTruth")) || !parent.activeInHierarchy)
            {
                return;
            }

            uint id;
            if (parent.layer == LayerMask.NameToLayer("NPC"))
            {
                var npcC = parent.GetComponent<NPCController>();
                id = npcC.GTID;
                CurrentIDs.Add(id);
            }
            else
            {
                return;
            }

          
        }
    }

}
