using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

// Test to verify Burst package is working
public class BurstTest : MonoBehaviour
{
    void Start()
    {
        Debug.Log("Burst package test: Starting...");

        // Test Burst-compiled job
        TestBurstJob();

        // Test Burst-compiled function
        TestBurstFunction();
    }

    private void TestBurstJob()
    {
        Debug.Log("Testing Burst-compiled job...");

        // Create data for processing
        var inputArray = new NativeArray<float>(10000, Allocator.TempJob);
        var outputArray = new NativeArray<float>(10000, Allocator.TempJob);

        // Fill input array
        for (int i = 0; i < inputArray.Length; i++)
        {
            inputArray[i] = i;
        }

        // Create and schedule Burst-compiled job
        var job = new BurstTestJob
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
            float expected = math.sin(inputArray[i]) * math.cos(inputArray[i]);
            if (math.abs(outputArray[i] - expected) > 0.001f)
            {
                success = false;
                break;
            }
        }

        if (success)
        {
            Debug.Log("✅ Burst job completed successfully!");
        }
        else
        {
            Debug.LogError("❌ Burst job failed!");
        }

        // Clean up
        inputArray.Dispose();
        outputArray.Dispose();
    }

    private void TestBurstFunction()
    {
        Debug.Log("Testing Burst-compiled function...");
        
        float result = BurstMathFunctions.ComplexCalculation(42f);
        Debug.Log($"✅ Burst function result: {result}");
    }

}

// Burst-compiled job that performs complex math operations
[BurstCompile]
public struct BurstTestJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float> input;
    [WriteOnly] public NativeArray<float> output;

    public void Execute(int index)
    {
        // Complex math operations that benefit from Burst compilation
        float x = input[index];
        output[index] = math.sin(x) * math.cos(x);
    }
}
