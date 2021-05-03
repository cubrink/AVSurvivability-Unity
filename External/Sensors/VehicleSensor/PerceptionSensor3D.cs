// using System;
// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using Simulator.Bridge;
// using Simulator.Bridge.Data;
// using Simulator.Map;
// using Simulator.Utilities;
// using Simulator.Sensors.UI;

// namespace Simulator.Sensors
// {
//     /*
//      * Perception 3D Ground Truth Sensor
//      *
//      * This sensor looks at all objects within a fixed distance and publishes ground truth data about it
//      */
//     [SensorType("3D Ground Truth", new[] { typeof(Detected3DObjectData) })]
//     public class PerceptionSensor3D : SensorBase
//     {
//         /*
//          * SensorParameter: Frequency - Defines the maximum rate that messages will be published
//          */
//         [SensorParameter]
//         [Range(1f, 100f)]
//         public float Frequency = 10.0f;

//         /*
//          * SensorParameter: MaxDistance - Defines the how close an object must be to the sensor to be detected
//          */
//         [SensorParameter]
//         [Range(1f, 1000f)]
//         public float MaxDistance = 100.0f;

//         /*
//          * Triggers callback when object enters predefined range
//          */
//         public RangeTrigger RangeTrigger;
//         WireframeBoxes WireframeBoxes;

//         /*
//          * Used to publish data to AV stack
//          */
//         private BridgeInstance Bridge;
//         private Publisher<Detected3DObjectData> Publish;

    
//         /*
//          * Holds information on objects that have been seen by the sensor
//          */
//         private Dictionary<uint, Tuple<Detected3DObject, Collider>> Detected;
//         private HashSet<uint> CurrentIDs;

//         [AnalysisMeasurement(MeasurementType.Count)]
//         public int MaxTracked = -1;


//         /*
//          * Mark the resource usage of the sensor
//          */
//         public override SensorDistributionType DistributionType => SensorDistributionType.HighLoad;
//         MapOrigin MapOrigin;


        
//         /*
//          * OnBridgeSetup()
//          *
//          * Defines behavior required to initialize the bridge
//          */
//         public override void OnBridgeSetup(BridgeInstance bridge)
//         {
//             Bridge = bridge;
//             Publish = Bridge.AddPublisher<Detected3DObjectData>(Topic);
//         }

//         /*
//          * Start()
//          *
//          * Called when sensor is created
//          */
//         void Start()
//         {
//             WireframeBoxes = SimulatorManager.Instance.WireframeBoxes;


//             // Initializes the RangeTrigger
//             if (RangeTrigger == null)
//             {
//                 // Returns the component of RangeTrigger type in the GameObject or any of its children
//                 // This function is inherited from SensorBehavior -> MonoBehavior -> Behavior -> Component.GetComponentInChildren()
//                 RangeTrigger = GetComponentInChildren<RangeTrigger>();
//             }

//             // Define Callback for RangeTrigger. Use WhileInRange when object becomes in range
//             RangeTrigger.SetCallbacks(WhileInRange);

//             // Define distance that Callback gets triggered by
//             RangeTrigger.transform.localScale = MaxDistance * Vector3.one;

//             MapOrigin = MapOrigin.Find();

//             // Initialize to allow tracking
//             Detected = new Dictionary<uint, Tuple<Detected3DObject, Collider>>();
//             CurrentIDs = new HashSet<uint>();

//             // Starts a Coroutine. A coroutine in C# is like a generator in Python
//             // They allow for intermediary stopping and resuming without losing state
//             // Values are produced by the coroutine (or generator) using the yield statement
//             StartCoroutine(OnPublish());
//         }

//         /*
//          * FixedUpdate()
//          *
//          * Called at a specified rate, consistent with real time, not game time
//          */
//         private void FixedUpdate()
//         {
//             MaxTracked = Math.Max(MaxTracked, CurrentIDs.Count);
//             CurrentIDs.Clear();
//         }

//         /*
//          * WhileInRange
//          * 
//          * Called when GameObject comes in range of the EGO vehicle
//          */
//         void WhileInRange(Collider other)
//         {
//             // Get GameObject for both the ego vehicle and the object in range
//             GameObject egoGO = transform.parent.gameObject;
//             GameObject parent = other.transform.parent.gameObject;

//             // Exit if they are the same object
//             if (parent == egoGO)
//             {
//                 return;
//             }

//             // Exits if any of the following are not met
//             // Note that the first condition should NEVER be true
//             // The RangeTrigger was configured to only trigger for objects on layer GroundTruth
//             // The second condition skips objects that are not active in scene
//             if (!(other.gameObject.layer == LayerMask.NameToLayer("GroundTruth")) || !parent.activeInHierarchy)
//             {
//                 return;
//             }

//             // Declare variable to hold various information about the object in range
//             uint id;
//             string label;
//             Vector3 velocity;
//             float angular_speed;  // Angular speed around up axis of objects, in radians/sec
//             if (parent.layer == LayerMask.NameToLayer("Agent"))
//             {
//                 // Store data from other EGO vehicles
//                 var egoC = parent.GetComponent<VehicleController>();
//                 var rb = parent.GetComponent<Rigidbody>();
//                 id = egoC.GTID;
//                 label = "Sedan";
//                 velocity = rb.velocity;
//                 angular_speed = rb.angularVelocity.y;
//             }
//             else if (parent.layer == LayerMask.NameToLayer("NPC"))
//             {
//                 // Store data from NPC vehicles
//                 var npcC = parent.GetComponent<NPCController>();
//                 id = npcC.GTID;
//                 label = npcC.NPCLabel;
//                 velocity = npcC.GetVelocity();
//                 angular_speed = npcC.GetAngularVelocity().y;
//             }
//             else if (parent.layer == LayerMask.NameToLayer("Pedestrian"))
//             {
//                 // Get data from pedestrians
//                 var pedC = parent.GetComponent<PedestrianController>();
//                 id = pedC.GTID;
//                 label = "Pedestrian";
//                 velocity = pedC.CurrentVelocity;
//                 angular_speed = pedC.CurrentAngularVelocity.y;
//             }
//             else
//             {
//                 // Unknown case, return
//                 return;
//             }

//             Vector3 size = ((BoxCollider)other).size;
//             if (size.magnitude == 0)
//             {
//                 return;
//             }

//             // Linear speed in forward direction of objects, in meters/sec
//             float speed = Vector3.Dot(velocity, parent.transform.forward);
//             // Local position of object in ego local space
//             Vector3 relPos = transform.InverseTransformPoint(parent.transform.position);
//             // Relative rotation of objects wrt ego frame
//             Quaternion relRot = Quaternion.Inverse(transform.rotation) * parent.transform.rotation;

//             var mapRotation = MapOrigin.transform.localRotation;
//             velocity = Quaternion.Inverse(mapRotation) * velocity;
//             var heading = parent.transform.localEulerAngles.y - mapRotation.eulerAngles.y;

//             // Center of bounding box
//             GpsLocation location = MapOrigin.GetGpsLocation(((BoxCollider)other).bounds.center);
//             GpsData gps = new GpsData()
//             {
//                 Easting = location.Easting,
//                 Northing = location.Northing,
//                 Altitude = location.Altitude,
//             };

//             if (!Detected.ContainsKey(id))
//             {
//                 var det = new Detected3DObject()
//                 {
//                     Id = id,
//                     Label = label,
//                     Score = 1.0f,
//                     Position = relPos,
//                     Rotation = relRot,
//                     Scale = size,
//                     LinearVelocity = new Vector3(speed, 0, 0),
//                     AngularVelocity = new Vector3(0, 0, angular_speed),
//                     Velocity = velocity,
//                     Gps = gps,
//                     Heading = heading,
//                     TrackingTime = 0f,
//                 };

//                 Detected.Add(id, new Tuple<Detected3DObject, Collider>(det, other));
//             }
//             else
//             {
//                 var det = Detected[id].Item1;
//                 det.Position = relPos;
//                 det.Rotation = relRot;
//                 det.LinearVelocity = new Vector3(speed, 0, 0);
//                 det.AngularVelocity = new Vector3(0, 0, angular_speed);
//                 det.Acceleration = (velocity - det.Velocity) / Time.fixedDeltaTime;
//                 det.Velocity = velocity;
//                 det.Gps = gps;
//                 det.Heading = heading;
//                 det.TrackingTime += Time.fixedDeltaTime;
//             }

//             CurrentIDs.Add(id);
//         }

//         private IEnumerator OnPublish()
//         {
//             uint seqId = 0;
//             double nextSend = SimulatorManager.Instance.CurrentTime + 1.0f / Frequency;

//             while (true)
//             {
//                 yield return new WaitForFixedUpdate();

//                 var IDs = new HashSet<uint>(Detected.Keys);
//                 IDs.ExceptWith(CurrentIDs);
//                 foreach(uint id in IDs)
//                 {
//                     Detected.Remove(id);
//                 }

//                 if (Bridge != null && Bridge.Status == Status.Connected)
//                 {
//                     if (SimulatorManager.Instance.CurrentTime < nextSend)
//                     {
//                         continue;
//                     }
//                     nextSend = SimulatorManager.Instance.CurrentTime + 1.0f / Frequency;

//                     var currentObjects = new List<Detected3DObject>();
//                     foreach (uint id in CurrentIDs)
//                     {
//                         currentObjects.Add(Detected[id].Item1);
//                     }

//                     var data = new Detected3DObjectData()
//                     {
//                         Name = Name,
//                         Frame = Frame,
//                         Time = SimulatorManager.Instance.CurrentTime,
//                         Sequence = seqId++,
//                         Data = currentObjects.ToArray(),
//                     };

//                     Publish(data);
//                 }
//             }
//         }

//         public override void OnVisualize(Visualizer visualizer)
//         {
//             foreach (uint id in CurrentIDs)
//             {
//                 var col = Detected[id].Item2;
//                 if (col.gameObject.activeInHierarchy)
//                 {
//                     GameObject parent = col.gameObject.transform.parent.gameObject;
//                     Color color = Color.green;
//                     if (parent.layer == LayerMask.NameToLayer("Pedestrian"))
//                     {
//                         color = Color.yellow;
//                     }

//                     BoxCollider box = col as BoxCollider;
//                     WireframeBoxes.Draw
//                     (
//                         box.transform.localToWorldMatrix,
//                         new Vector3(0f, box.bounds.extents.y, 0f),
//                         box.size,
//                         color
//                     );
//                 }
//             }
//         }

//         public override void OnVisualizeToggle(bool state) {}

//         public bool CheckVisible(Bounds bounds)
//         {
//             return Vector3.Distance(transform.position, bounds.center) < 50f;
//             //var activeCameraPlanes = Utility.CalculateFrustum(transform.position, (bounds.center - transform.position).normalized);
//             //return GeometryUtility.TestPlanesAABB(activeCameraPlanes, bounds);
//         }

//         void OnDestroy()
//         {
//             StopAllCoroutines();

//             Detected.Clear();
//             CurrentIDs.Clear();
//         }
//     }
// }