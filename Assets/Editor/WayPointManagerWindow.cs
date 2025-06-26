using UnityEngine;
using UnityEditor;

public class WayPointManagerWindow : EditorWindow
{
   [MenuItem("Waypoint/Waypoints Editor Tools")]
   public static void ShowWindow()
   {
        GetWindow<WayPointManagerWindow>("Waypoints Editor Tools");
   }

   public Transform waypointOrigin;

   private void OnGUI()
   {
        SerializedObject obj = new SerializedObject(this);

        EditorGUILayout.PropertyField(obj.FindProperty("waypointOrigin"));

        if(waypointOrigin == null)
        {
            EditorGUILayout.HelpBox("Please assign a Waypoint Origin Transform. ",MessageType.Warning);
        }
        else
        {
            EditorGUILayout.BeginVertical("Box");
            createButtons();
            EditorGUILayout.EndVertical();
        }

        obj.ApplyModifiedProperties();
   }

   void createButtons()
   {
        if(GUILayout.Button("Create Waypoint"))
        {
            CreateWaypoint();
        }
   }

   void CreateWaypoint()
   {
        GameObject waypointObject = new GameObject("Waypoint " + waypointOrigin.childCount, typeof(WayPoint));
        waypointObject.transform.SetParent(waypointOrigin, false);

        WayPoint waypoint = waypointObject.GetComponent<WayPoint>();

        if(waypointOrigin.childCount > 1)
        {
            waypoint.previousWaypoint = waypointOrigin.GetChild(waypointOrigin.childCount - 2).GetComponent<WayPoint>();
            waypoint.previousWaypoint.nextWaypoint = waypoint;

            waypoint.transform.position = waypoint.previousWaypoint.transform.position;
            waypoint.transform.forward = waypoint.previousWaypoint.transform.forward;
        }
        Selection.activeGameObject = waypoint.gameObject;
   }
}
