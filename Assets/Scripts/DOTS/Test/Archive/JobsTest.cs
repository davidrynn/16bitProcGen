using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

// Simple test to verify Jobs package is working
public class JobsTest : MonoBehaviour
{
    void Start()
    {
        Debug.Log("Jobs package test: Starting...");
        
        // Test basic Jobs functionality
        TestSimpleJob();
        TestParallelJob();
    }
    
    private void TestSimpleJob()
    {
        Debug.Log("Testing simple job...");
        
        // Create a simple job
        var job = new SimpleTestJob
        {
            input = 42,
            output = new NativeArray<int>(1, Allocator.TempJob)
        };
        
        // Schedule and complete the job
        var handle = job.Schedule();
        handle.Complete();
        
        Debug.Log($"✅ Simple job completed! Result: {job.output[0]}");
        
        // Clean up
        job.output.Dispose();
    }
    
    private void TestParallelJob()
    {
        Debug.Log("Testing parallel job...");
        
        // Create data for parallel processing
        var inputArray = new NativeArray<float>(1000, Allocator.TempJob);
        var outputArray = new NativeArray<float>(1000, Allocator.TempJob);
        
        // Fill input array
        for (int i = 0; i < inputArray.Length; i++)
        {
            inputArray[i] = i;
        }
        
        // Create and schedule parallel job
        var job = new ParallelTestJob
        {
            input = inputArray,
            output = outputArray
        };
        
        var handle = job.Schedule(inputArray.Length, 64);
        handle.Complete();
        
        // Verify results
        bool success = true;
        for (int i = 0; i < outputArray.Length; i++)
        {
            if (outputArray[i] != inputArray[i] * 2f)
            {
                success = false;
                break;
            }
        }
        
        if (success)
        {
            Debug.Log("✅ Parallel job completed successfully!");
        }
        else
        {
            Debug.LogError("❌ Parallel job failed!");
        }
        
        // Clean up
        inputArray.Dispose();
        outputArray.Dispose();
    }
}

// Simple job that doubles a number
public struct SimpleTestJob : IJob
{
    public int input;
    public NativeArray<int> output;
    
    public void Execute()
    {
        output[0] = input * 2;
    }
}

// Parallel job that doubles each element in an array
public struct ParallelTestJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float> input;
    [WriteOnly] public NativeArray<float> output;
    
    public void Execute(int index)
    {
        output[index] = input[index] * 2f;
    }
}