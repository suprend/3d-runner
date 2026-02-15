using System.Collections.Generic;
using UnityEngine;

public class RunnerEndlessTrack : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private float segmentLength = 20f;
    [SerializeField] private float recycleBehindPlayerDistance = 40f;
    [SerializeField] private List<Transform> segments = new();

    private float nextZ;
    private float minRecycleSpawnZOffset;
    private readonly Queue<Transform> recycleQueue = new();
    private readonly List<Transform> sortedSegmentsBuffer = new();

    public void Initialize(Transform playerTransform, float trackSegmentLength, float recycleBehindDistance, float minRecycleSpawnOffset, List<Transform> trackSegments)
    {
        player = playerTransform;
        segmentLength = Mathf.Max(1f, trackSegmentLength);
        recycleBehindPlayerDistance = Mathf.Max(segmentLength, recycleBehindDistance);
        minRecycleSpawnZOffset = Mathf.Max(0f, minRecycleSpawnOffset);
        segments = trackSegments ?? new List<Transform>();

        RebuildRecycleQueue();
    }

    private void Update()
    {
        if (player == null) return;
        if (recycleQueue.Count == 0) return;

        float playerZ = player.position.z;

        int safety = recycleQueue.Count;
        while (safety-- > 0 && recycleQueue.Count > 0)
        {
            var seg = recycleQueue.Peek();
            if (seg == null)
            {
                recycleQueue.Dequeue();
                continue;
            }

            if (playerZ - seg.position.z <= recycleBehindPlayerDistance) break;

            recycleQueue.Dequeue();

            float newZ = Mathf.Max(nextZ, playerZ + minRecycleSpawnZOffset);
            var p = seg.position;
            p.z = newZ;
            seg.position = p;
            nextZ = newZ + segmentLength;

            recycleQueue.Enqueue(seg);
        }
    }

    private void RebuildRecycleQueue()
    {
        recycleQueue.Clear();
        sortedSegmentsBuffer.Clear();

        if (segments == null || segments.Count == 0)
        {
            nextZ = 0f;
            return;
        }

        for (int i = 0; i < segments.Count; i++)
        {
            var seg = segments[i];
            if (seg != null) sortedSegmentsBuffer.Add(seg);
        }

        if (sortedSegmentsBuffer.Count == 0)
        {
            nextZ = 0f;
            return;
        }

        sortedSegmentsBuffer.Sort((a, b) => a.position.z.CompareTo(b.position.z));
        for (int i = 0; i < sortedSegmentsBuffer.Count; i++)
        {
            recycleQueue.Enqueue(sortedSegmentsBuffer[i]);
        }

        nextZ = sortedSegmentsBuffer[sortedSegmentsBuffer.Count - 1].position.z + segmentLength;
    }
}
