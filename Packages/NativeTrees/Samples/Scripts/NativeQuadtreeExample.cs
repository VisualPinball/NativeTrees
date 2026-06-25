using System;
using System.Collections.Generic;
using NativeTrees;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

#if UNITY_6000_5_OR_NEWER
using TreeObjectId = UnityEngine.EntityId;
#else
using TreeObjectId = System.Int32;
#endif

namespace NativeTrees.Samples
{
    public class NativeQuadtreeExample : MonoBehaviour
    {
        public int squareCount = 100;
        public GameObject squarePrefab;
        public Camera camera;
        
        private NativeQuadtree<TreeObjectId> quadtree;
        private Dictionary<TreeObjectId, QuadtreeSquare> squares = new Dictionary<TreeObjectId, QuadtreeSquare>();
        private NativeList<TreeObjectId> overlap;
        private NativeQueue<TreeObjectId> nearest;

        private Vector2 mouseDownPos;
        
        private void Start()
        {
            Vector2 camPos = camera.transform.position;
            Vector2 camExtents = .5f * new Vector2(camera.orthographicSize * camera.aspect, camera.orthographicSize);

            var bounds = new AABB2D(camPos - camExtents, camPos + camExtents);
            quadtree = new NativeQuadtree<TreeObjectId>(bounds, Allocator.Persistent);
            nearest = new NativeQueue<TreeObjectId>(Allocator.Persistent);
            overlap = new NativeList<TreeObjectId>(Allocator.Persistent);
            
            for (int i = 0; i < squareCount; i++)
            {
                Vector2 pos = new Vector2(
                    Random.Range(bounds.min.x, bounds.max.x) * Random.value, 
                    Random.Range(bounds.min.y, bounds.max.y) * Random.value
                    );
                
                var square = Instantiate(squarePrefab, pos, Quaternion.identity).GetComponent<QuadtreeSquare>();
                var squareId = GetObjectId(square);

                quadtree.Insert(squareId, square.Bounds);
                squares.Add(squareId, square);
            }
            
            Debug.Log("Enable game view gizmos!");
        }

        private void OnDestroy()
        {
            nearest.Dispose();
            overlap.Dispose();
            quadtree.Dispose();
        }

        private void Update()
        {
            Vector2 mousePos = camera.ScreenToWorldPoint(Input.mousePosition);
            if (Input.GetMouseButtonDown(0))
                mouseDownPos = mousePos;

            if (Input.GetMouseButton(0))
            {
                overlap.Clear();
                quadtree.RangeAABB(new AABB2D(Vector2.Min(mouseDownPos, mousePos), Vector2.Max(mouseDownPos, mousePos)), overlap);
            }

            /*
            if (quadtree.TryGetNearestAABB(mousePos, 100, out TreeObjectId nearestId))
            {
                if (squares.TryGetValue(prevNearest, out var prev))
                {
                    prev.Color = Color.white;
                }
                
                squares[nearestId].Color = Color.red;
                prevNearest = nearestId;
            }
            */


            // Get the 10 nearest squares to the mouse
            // we wouldn't have to use the hashset if the objects were points
            NativeParallelHashSet<TreeObjectId> set = new NativeParallelHashSet<TreeObjectId>(10, Allocator.Temp);
            nearest.Clear();
            var nearestTen = new NearestTen()
            {
                nearest = nearest,
                set = set
            };
            quadtree.Nearest(mousePos, 100, ref nearestTen, default(NativeQuadtreeExtensions.AABBDistanceSquaredProvider<TreeObjectId>));
            set.Dispose();
        }
        
        struct NearestTen : IQuadtreeNearestVisitor<TreeObjectId>
        {
            public NativeQueue<TreeObjectId> nearest;
            public NativeParallelHashSet<TreeObjectId> set;
            
            public bool OnVist(TreeObjectId obj)
            {
                if (set.Add(obj))
                    nearest.Enqueue(obj);
                
                return nearest.Count < 10;
            }
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying)
                return;
            Vector2 mousePos = camera.ScreenToWorldPoint(Input.mousePosition);
            
            // Nearest (red)
            Gizmos.color = Color.red;
            while (nearest.TryDequeue(out TreeObjectId squareId))
            {
                var square = squares[squareId];
                var size = square.Bounds.Size;
                Gizmos.DrawCube(square.transform.position, 1.25f * new Vector3(size.x, size.y));
            }
            
            // Draw blue boxes around range overlap
            Gizmos.color = Color.blue;
            for (int i = 0; i < overlap.Length; i++)
            {
                if (!squares.TryGetValue(overlap[i], out var square))
                    continue;

                var size = square.Bounds.Size;

                Gizmos.DrawCube(square.transform.position, 1.25f * new Vector3(size.x, size.y));
            }
            
            // Green box for raycast
      
            Gizmos.color = Color.green;
            Gizmos.DrawLine(mouseDownPos, mousePos);
            if (quadtree.RaycastAABB(new Ray2D(mouseDownPos, (mousePos - mouseDownPos).normalized), out var hit, 
                maxDistance: math.distance(mouseDownPos, mousePos)))
            {
                if (squares.TryGetValue(hit.obj, out var square))
                {
                    var size = square.Bounds.Size;
                    Gizmos.DrawCube(square.transform.position, 1.25f * new Vector3(size.x, size.y));
                }
            }

            Gizmos.color = Color.black;
            quadtree.DrawGizmos();
        }

        private static TreeObjectId GetObjectId(UnityEngine.Object obj)
        {
#if UNITY_6000_5_OR_NEWER
            return obj.GetEntityId();
#else
            return obj.GetInstanceID();
#endif
        }
    }
}
