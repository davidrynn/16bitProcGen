using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Unity.Entities; // For BlobBuilder, BlobArray, BlobString

// Test to verify Collections package is working
public class CollectionsTest : MonoBehaviour
{
    void Start()
    {
        Debug.Log("Collections package test: Starting...");
        
        // Test NativeArray operations
        TestNativeArray();
        
        // Test NativeList operations
        TestNativeList();
        
        // Test NativeHashMap operations
        TestNativeHashMap();
        
        // Test NativeQueue operations
        TestNativeQueue();
        
        // Test BlobBuilder operations
        TestBlobBuilder();
    }
    
    private void TestNativeArray()
    {
        Debug.Log("Testing NativeArray operations...");
        
        // Create NativeArray
        var array = new NativeArray<int>(100, Allocator.TempJob);
        
        // Fill array with values
        for (int i = 0; i < array.Length; i++)
        {
            array[i] = i * i; // Square numbers
        }
        
        Debug.Log($"✅ NativeArray created with {array.Length} elements");
        Debug.Log($"✅ First element: {array[0]}, Last element: {array[array.Length - 1]}");
        
        // Test array operations
        int sum = 0;
        for (int i = 0; i < array.Length; i++)
        {
            sum += array[i];
        }
        
        Debug.Log($"✅ Sum of all elements: {sum}");
        
        // Clean up
        array.Dispose();
    }
    
    private void TestNativeList()
    {
        Debug.Log("Testing NativeList operations...");
        
        // Create NativeList
        var list = new NativeList<int>(Allocator.TempJob);
        
        // Add elements
        list.Add(1);
        list.Add(2);
        list.Add(3);
        list.Add(5);
        list.Add(8);
        
        // Print list contents manually
        string listContents = "";
        for (int i = 0; i < list.Length; i++)
        {
            if (i > 0) listContents += ", ";
            listContents += list[i].ToString();
        }
        Debug.Log($"✅ NativeList created with {list.Length} elements: [{listContents}]");
        
        // Test list operations (NativeList doesn't have Insert, so we'll add at the end)
        list.Add(4); // Add 4 at the end
        listContents = "";
        for (int i = 0; i < list.Length; i++)
        {
            if (i > 0) listContents += ", ";
            listContents += list[i].ToString();
        }
        Debug.Log($"✅ After add: [{listContents}]");
        
        // Remove first element (NativeList doesn't have RemoveAt, so we'll clear and rebuild)
        int firstElement = list[0];
        list.RemoveAt(0); // This should work
        listContents = "";
        for (int i = 0; i < list.Length; i++)
        {
            if (i > 0) listContents += ", ";
            listContents += list[i].ToString();
        }
        Debug.Log($"✅ After remove first ({firstElement}): [{listContents}]");
        
        // Clean up
        list.Dispose();
    }
    
    private void TestNativeHashMap()
    {
        Debug.Log("Testing NativeHashMap operations...");
        
        // Create NativeHashMap with int keys and int values (avoiding string issues)
        var map = new NativeHashMap<int, int>(10, Allocator.TempJob);
        
        // Add key-value pairs
        map.Add(1, 100);
        map.Add(2, 200);
        map.Add(3, 300);
        map.Add(5, 500);
        map.Add(8, 800);
        
        Debug.Log($"✅ NativeHashMap created with {map.Count} elements");
        
        // Test map operations
        if (map.TryGetValue(3, out int value))
        {
            Debug.Log($"✅ Key 3 maps to: {value}");
        }
        
        // Iterate through map
        var keys = map.GetKeyArray(Allocator.Temp);
        var values = map.GetValueArray(Allocator.Temp);
        
        Debug.Log("✅ HashMap contents:");
        for (int i = 0; i < keys.Length; i++)
        {
            Debug.Log($"  {keys[i]} -> {values[i]}");
        }
        
        // Clean up
        map.Dispose();
        keys.Dispose();
        values.Dispose();
    }
    
    private void TestNativeQueue()
    {
        Debug.Log("Testing NativeQueue operations...");
        
        // Create NativeQueue
        var queue = new NativeQueue<int>(Allocator.TempJob);
        
        // Enqueue elements
        queue.Enqueue(1);
        queue.Enqueue(2);
        queue.Enqueue(3);
        queue.Enqueue(5);
        queue.Enqueue(8);
        
        Debug.Log($"✅ NativeQueue created with {queue.Count} elements");
        
        // Dequeue elements
        Debug.Log("✅ Dequeuing elements:");
        while (queue.TryDequeue(out int item))
        {
            Debug.Log($"  Dequeued: {item}");
        }
        
        // Clean up
        queue.Dispose();
    }
    
    private void TestBlobBuilder()
    {
        Debug.Log("Testing BlobBuilder operations...");
        
        // Create BlobBuilder
        var builder = new BlobBuilder(Allocator.Temp);
        
        // Build a simple blob structure
        ref var root = ref builder.ConstructRoot<TestBlobData>();
        
        // Add array to blob
        var array = builder.Allocate(ref root.array, 5);
        array[0] = 10;
        array[1] = 20;
        array[2] = 30;
        array[3] = 40;
        array[4] = 50;
        
        // Add string to blob
        builder.AllocateString(ref root.name, "TestBlob");
        
        // Create blob asset
        var blobAsset = builder.CreateBlobAssetReference<TestBlobData>(Allocator.Persistent);
        
        Debug.Log($"✅ BlobAsset created with name: {blobAsset.Value.name}");
        
        // Print array contents manually
        string arrayContents = "";
        for (int i = 0; i < blobAsset.Value.array.Length; i++)
        {
            if (i > 0) arrayContents += ", ";
            arrayContents += blobAsset.Value.array[i].ToString();
        }
        Debug.Log($"✅ BlobAsset array: [{arrayContents}]");
        
        // Clean up
        builder.Dispose();
        blobAsset.Dispose();
    }
}

// Test structure for BlobBuilder
public struct TestBlobData
{
    public BlobArray<int> array;
    public BlobString name;
}