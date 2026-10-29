//
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Text;
using System.Collections.Concurrent;
using Newtonsoft.Json;
using System.Diagnostics;
using NAudio.Wave;

// 用于接收面部表情数据的类
[System.Serializable]
public class BodyFaceStreamData
{
    public List<List<List<float>>> pose; // Format: [frame][bone_index][x, y, z, w]
    public List<List<float>> trans; // Format: [frame][x, y, z] for Root_M translation
    public List<List<float>> blendshape; // Format: [frame][51] - ARKit前51个blendshape
    public List<float> audio; // 音频数据
}

// 面部动画帧结构
public struct BodyFaceAnimationFrame
{
    public Quaternion[] rotations;
    public Vector3 translation;
    public float[] blendshapeWeights; // 51个blendshape权重
}

public class StreamingServerFaceBodyAutoStartPushWav : MonoBehaviour
{
    [Header("Network Settings")]
    public int serverPort = 12346;
    public bool autoStartServer = true;
    [Header("Python Script Settings")]
    public string pythonExePath = @"C:\Users\cbh\.conda\envs\qwen\python.exe";
    public string pythonScriptPath = @"C:\Users\cbh\Desktop\pushwav2server\pushwav2server.py";

    [Header("Face Objects Setup")]
    [Tooltip("Drag in the 3 face objects to control blendshape")]
    public SkinnedMeshRenderer faceObject1;
    public SkinnedMeshRenderer faceObject2;
    public SkinnedMeshRenderer faceObject3;
    public SkinnedMeshRenderer faceObject4;
    public SkinnedMeshRenderer faceObject5;

    [Header("Blendshape Settings")]
    [Tooltip("ARKit blendshape count")]
    public int blendshapeCount = 51;

    [Header("Character Setup")]
    public Transform characterRoot; // Assign the 'LaForgeFemale' GameObject here in the Inspector

    [Header("Initial Pose")]
    public bool captureInitialPose = true;
    public KeyCode captureInitialPoseKey = KeyCode.Space;

    // IMPORTANT: This list MUST match the order of bones from your BVH file.
    // You can get this from python: print(bvh.load(bvh_file_path)['names'])
    private readonly string[] bvhBoneNames = new string[] {
       "Root_M", "Hip_R", "HipPart1_R", "Knee_R", "KneePart1_R",
       "Ankle_R", "Toes_R", "ToesEnd_R", "Heel_R", "HeelEnd_R",
       "Spine1_M", "Spine1Part1_M", "Chest_M", "Scapula_R", "Shoulder_R",
       "ShoulderPart1_R", "Elbow_R", "ElbowPart1_R", "Wrist_R",
       "MiddleFinger1_R", "MiddleFinger2_R", "MiddleFinger3_R",
       "MiddleFinger4_R", "ThumbFinger1_R", "ThumbFinger2_R",
       "ThumbFinger3_R", "ThumbFinger4_R", "IndexFinger1_R",
       "IndexFinger2_R", "IndexFinger3_R", "IndexFinger4_R", "Cup_R",
       "PinkyFinger1_R", "PinkyFinger2_R", "PinkyFinger3_R",
       "PinkyFinger4_R", "RingFinger1_R", "RingFinger2_R",
       "RingFinger3_R", "RingFinger4_R", "Neck_M", "NeckPart1_M",
       "Head_M", "Head_angleFix", "L_eye_jnt", "L_eScale_jnt", "Eye_L",
       "L_pupil_jnt", "R_eye_jnt", "R_eScale_jnt", "Eye_R", "R_pupil_jnt",
       "Scapula_L", "Shoulder_L", "ShoulderPart1_L", "Elbow_L",
       "ElbowPart1_L", "Wrist_L", "MiddleFinger1_L", "MiddleFinger2_L",
       "MiddleFinger3_L", "MiddleFinger4_L", "ThumbFinger1_L",
       "ThumbFinger2_L", "ThumbFinger3_L", "ThumbFinger4_L",
       "IndexFinger1_L", "IndexFinger2_L", "IndexFinger3_L",
       "IndexFinger4_L", "Cup_L", "PinkyFinger1_L", "PinkyFinger2_L",
       "PinkyFinger3_L", "PinkyFinger4_L", "RingFinger1_L",
       "RingFinger2_L", "RingFinger3_L", "RingFinger4_L", "Hip_L",
       "HipPart1_L", "Knee_L", "KneePart1_L", "Ankle_L", "Toes_L",
       "ToesEnd_L", "Heel_L", "HeelEnd_L"
    };

    private Dictionary<string, Transform> boneMap;
    private Transform[] orderedBoneTransforms;
    private Transform rootBone; // Reference to Root_M for translation

    // Store initial pose for proper rotation calculation
    // 加这个initialRotations的原因是，fbx人物导入unity后，虽说姿态和bvh的rest pose一样
    // 但是每个关节的旋转不像blender一样都是0，而是有一个奇怪的数值，所以我们在这个基础上做变换
    private Quaternion[] initialRotations;
    private Vector3 initialRootPosition;
    private bool initialPoseCaptured = false;
    // --- Networking ---
    private TcpListener tcpListener;
    private TcpClient connectedClient;
    private NetworkStream stream;
    private Thread serverThread;
    private volatile bool isRunning = false;

    [Header("Server Status")]
    public bool isServerRunning = false;
    public bool isClientConnected = false;

    // --- Thread-safe Queues for Main Thread ---
    private ConcurrentQueue<BodyFaceStreamData> dataQueue = new ConcurrentQueue<BodyFaceStreamData>();

    // --- NAudio 低延迟音频播放 ---
    private WaveOutEvent waveOut;
    private BufferedWaveProvider waveProvider;

    // --- Animation Playback ---
    private List<BodyFaceAnimationFrame> animationBuffer = new List<BodyFaceAnimationFrame>();
    public float frameTimer = 0.0f;
    private const float FRAME_RATE = 60.0f; // 60fps for blendshape
    private Process pythonProcess;

    [Header("Debug Info")]
    public int currentBufferSize = 0;
    public int audioBufferSize = 0;

    public float root_x, root_y, root_z;

    public float root_euler_x = 0, root_euler_y = 180, root_euler_z = 0;

    public float root_p_x = 1, root_p_y = 1, root_p_z = -1;


    public int motion_frame_delay = 10; // 用于延迟处理的帧数

    public int frame_index = 0;



    void Start()
    {
        Application.runInBackground = true;
        
        // 初始化 NAudio 低延迟音频系统
        InitSystemAudio();

        MapBones();
        // 验证面部对象
        ValidateFaceObjects();

        if (captureInitialPose)
        {
            CaptureInitialPose();
        }

        if (autoStartServer)
        {
            StartServer();
        }

        for (int i = 0; i < motion_frame_delay; i++)
        {
            Quaternion[] rotations = new Quaternion[orderedBoneTransforms.Length];
            for (int j = 0; j < orderedBoneTransforms.Length; j++)
            {
                rotations[i] = new Quaternion(0, 0, 0, 1);
            }
            BodyFaceAnimationFrame frame = new BodyFaceAnimationFrame
            {
                blendshapeWeights = new float[blendshapeCount],
                rotations = rotations,
                translation = new Vector3(0, 0, 0)
            };
            for (int j = 0; j < blendshapeCount; j++)
            {
                frame.blendshapeWeights[j] = 0f; // 初始化为0
            }
            animationBuffer.Add(frame);
        }
    }

    // 初始化 NAudio 系统音频（低延迟）
    void InitSystemAudio()
    {
        try
        {
            // 24kHz, 单声道, 32-bit float
            var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(24000, 1);
            waveProvider = new BufferedWaveProvider(waveFormat)
            {
                BufferDuration = TimeSpan.FromMilliseconds(1000),  // 最大缓冲1秒
                DiscardOnBufferOverflow = true  // 溢出时丢弃旧数据
            };

            // 使用 WaveOutEvent
            waveOut = new WaveOutEvent();
            waveOut.DesiredLatency = 100;  // 目标延迟 100ms（更稳定）
            waveOut.NumberOfBuffers = 3;   // 使用3个缓冲区
            waveOut.Init(waveProvider);
            waveOut.Play();
            
            UnityEngine.Debug.Log($"NAudio system audio init success! format: {waveFormat}, delay: 100ms");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"NAudio init fail: {e.Message}\n{e.StackTrace}");
        }
    }

    // 推送音频样本到系统音频缓冲区
    void PushAudioSamples(List<float> samples)
    {
        if (waveProvider == null || samples == null || samples.Count == 0) return;
        
        // 检测数据范围，判断是否需要归一化
        float maxVal = 0;
        foreach (var s in samples) maxVal = Mathf.Max(maxVal, Mathf.Abs(s));
        
        // 调试日志
        if (frame_index % 60 == 0)
        {
            UnityEngine.Debug.Log($"[Audio] push {samples.Count} samples, max amplitude: {maxVal:F4}");
        }
        
        // float[] 转 byte[] (32-bit float = 4 bytes)
        byte[] buffer = new byte[samples.Count * 4];
        for (int i = 0; i < samples.Count; i++)
        {
            float sample = samples[i];
            
            // 如果最大值超过1.0，假设是16-bit PCM整数格式，需要归一化
            if (maxVal > 1.0f)
            {
                sample = sample / 32768.0f;  // 转换为 -1.0 到 1.0
            }
            
            byte[] bytes = BitConverter.GetBytes(sample);
            Buffer.BlockCopy(bytes, 0, buffer, i * 4, 4);
        }
        waveProvider.AddSamples(buffer, 0, buffer.Length);
    }

    // 清理系统音频资源
    void CleanupSystemAudio()
    {
        try
        {
            waveOut?.Stop();
            waveOut?.Dispose();
            waveOut = null;
            waveProvider = null;
            UnityEngine.Debug.Log("NAudio clean finish");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"NAudio clean fail: {e.Message}");
        }
    }

    void ValidateFaceObjects()
    {
        // 验证每个面部对象是否正确设置
        SkinnedMeshRenderer[] faceObjects = { faceObject1, faceObject2, faceObject3, faceObject4, faceObject5 };

        for (int i = 0; i < 4; i++)
        {
            if (faceObjects[i] != null && faceObjects[i].sharedMesh != null)
            {
                var mesh = faceObjects[i].sharedMesh;
                int blendshapeCountInMesh = mesh.blendShapeCount;

                if (blendshapeCountInMesh < blendshapeCount)
                {
                    UnityEngine.Debug.LogWarning($"Face object {i + 1} only has {blendshapeCountInMesh} blendshapes, but we need {blendshapeCount}. Will only use available blendshapes.");
                }
                else
                {
                    UnityEngine.Debug.Log($"Face object {i + 1} has {blendshapeCountInMesh} blendshapes, will use first {blendshapeCount}.");
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning($"Face object {i + 1} is not assigned or has no mesh!");
            }
        }
    }


    void MapBones()
    {
        if (characterRoot == null)
        {
            UnityEngine.Debug.LogError("Character Root is not assigned!");
            return;
        }

        boneMap = new Dictionary<string, Transform>();
        var allChildren = characterRoot.GetComponentsInChildren<Transform>();
        foreach (var child in allChildren)
        {
            boneMap[child.name] = child;
        }

        // Create an ordered array of transforms for fast lookup during animation
        orderedBoneTransforms = new Transform[bvhBoneNames.Length];
        for (int i = 0; i < bvhBoneNames.Length; i++)
        {
            string boneName = bvhBoneNames[i];
            if (boneMap.ContainsKey(boneName))
            {
                orderedBoneTransforms[i] = boneMap[boneName];
            }
            else
            {
                UnityEngine.Debug.LogWarning($"Bone '{boneName}' not found in character hierarchy!");
            }
        }

        // Store reference to Root_M for translation
        if (boneMap.ContainsKey("Root_M"))
        {
            rootBone = boneMap["Root_M"];
        }
        else
        {
            UnityEngine.Debug.LogError("Root_M bone not found! Translation will not work.");
        }

        UnityEngine.Debug.Log("Bone mapping complete.");
    }

    void CaptureInitialPose()
    {
        if (orderedBoneTransforms == null)
        {
            UnityEngine.Debug.LogError("Cannot capture initial pose: bone mapping not complete!");
            return;
        }

        initialRotations = new Quaternion[orderedBoneTransforms.Length];

        for (int i = 0; i < orderedBoneTransforms.Length; i++)
        {
            if (orderedBoneTransforms[i] != null)
            {
                initialRotations[i] = orderedBoneTransforms[i].localRotation;
            }
            else
            {
                initialRotations[i] = Quaternion.identity;
            }
        }

        if (rootBone != null)
        {
            initialRootPosition = rootBone.localPosition;
        }

        initialPoseCaptured = true;
        UnityEngine.Debug.Log("Initial pose captured successfully!");
    }

    public void ResetToInitialPose()
    {
        if (!initialPoseCaptured)
        {
            UnityEngine.Debug.LogWarning("Initial pose not captured yet!");
            return;
        }

        for (int i = 0; i < orderedBoneTransforms.Length; i++)
        {
            if (orderedBoneTransforms[i] != null)
            {
                orderedBoneTransforms[i].localRotation = initialRotations[i];
            }
        }

        if (rootBone != null)
        {
            rootBone.localPosition = initialRootPosition;
        }

        UnityEngine.Debug.Log("Reset to initial pose");
    }

    public void StartServer()
    {
        if (isRunning)
        {
            UnityEngine.Debug.LogWarning("Server is already running!");
            return;
        }

        serverThread = new Thread(new ThreadStart(ServerLoop));
        serverThread.IsBackground = true;
        serverThread.Start();

        UnityEngine.Debug.Log($"Starting TCP server on port {serverPort}...");
    }

    public void StopServer()
    {
        isRunning = false;

        if (stream != null)
        {
            stream.Close();
            stream = null;
        }

        if (connectedClient != null)
        {
            connectedClient.Close();
            connectedClient = null;
        }

        if (tcpListener != null)
        {
            tcpListener.Stop();
            tcpListener = null;
        }

        if (serverThread != null && serverThread.IsAlive)
        {
            serverThread.Join(1000); // 等待最多1秒让线程结束
        }

        isServerRunning = false;
        isClientConnected = false;
        UnityEngine.Debug.Log("Server stopped.");
    }

    private void ServerLoop()
    {
        try
        {
            // 创建并启动TCP监听器
            tcpListener = new TcpListener(IPAddress.Any, serverPort);
            tcpListener.Start();
            isRunning = true;
            isServerRunning = true;

            UnityEngine.Debug.Log($"TCP Server started on port {serverPort}. Waiting for client connection...");

            // 等待客户端连接
            connectedClient = tcpListener.AcceptTcpClient();
            stream = connectedClient.GetStream();
            isClientConnected = true;
            
            UnityEngine.Debug.Log("Client connected! Starting to receive data...");
            // 启动Python脚本
            StartPythonScript();

            byte[] headerBuffer = new byte[4];

            while (isRunning && connectedClient.Connected)
            {
                try
                {
                    // 1. 读取4字节头部获取消息大小
                    int bytesRead = 0;
                    while (bytesRead < 4)
                    {
                        int read = stream.Read(headerBuffer, bytesRead, 4 - bytesRead);
                        if (read == 0)
                        {
                            // 客户端断开连接
                            UnityEngine.Debug.Log("Client disconnected.");
                            break;
                        }
                        bytesRead += read;
                    }

                    if (bytesRead < 4) break; // 如果无法读取头部则退出

                    // 转换头部为整数
                    if (BitConverter.IsLittleEndian) Array.Reverse(headerBuffer);
                    int messageSize = BitConverter.ToInt32(headerBuffer, 0);

                    // 2. 读取完整消息
                    byte[] messageBuffer = new byte[messageSize];
                    bytesRead = 0;
                    while (bytesRead < messageSize)
                    {
                        int read = stream.Read(messageBuffer, bytesRead, messageSize - bytesRead);
                        if (read == 0)
                        {
                            // 客户端断开连接
                            UnityEngine.Debug.Log("Client disconnected while reading message.");
                            break;
                        }
                        bytesRead += read;
                    }

                    if (bytesRead < messageSize) break; // 如果无法读取完整消息则退出

                    string jsonMessage = Encoding.UTF8.GetString(messageBuffer);
                    BodyFaceStreamData receivedData = JsonConvert.DeserializeObject<BodyFaceStreamData>(jsonMessage);

                    // 将数据加入队列供主线程处理
                    dataQueue.Enqueue(receivedData);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError("Error reading data: " + e.Message);
                    break;
                }
            }
        }
        catch (SocketException e)
        {
            UnityEngine.Debug.LogError("SocketException: " + e.ToString());
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError("Exception: " + e.ToString());
        }
        finally
        {
            // 清理
            isRunning = false;
            isClientConnected = false;

            if (stream != null)
            {
                stream.Close();
                stream = null;
            }

            if (connectedClient != null)
            {
                connectedClient.Close();
                connectedClient = null;
            }

            if (tcpListener != null)
            {
                tcpListener.Stop();
                tcpListener = null;
            }

            UnityEngine.Debug.Log("Server thread ended.");
        }
    }

    private void StartPythonScript()
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = pythonExePath,
                Arguments = $"\"{pythonScriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = false
            };

            pythonProcess = Process.Start(startInfo);
            UnityEngine.Debug.Log("Python script started!");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to start Python script: {e.Message}");
        }
    }

    private void ClearBuffers()
    {
        // 清空音频缓冲区
        if (waveProvider != null)
        {
            waveProvider.ClearBuffer();
        }

        // 清空动画缓冲区，只保留最后motion_frame_delay个帧
        if (animationBuffer.Count > motion_frame_delay)
        {
            int framesToRemove = animationBuffer.Count - motion_frame_delay;
            animationBuffer.RemoveRange(0, framesToRemove);
        }

        UnityEngine.Debug.Log($"Buffers cleared! Audio buffer emptied, Animation buffer kept last {motion_frame_delay} frames.");
    }
    void Update()
    {
        // 检测C键按下，清空缓冲区
        if (Input.GetKeyDown(KeyCode.C))
        {
            ClearBuffers();
        }


        // 处理自上一帧以来到达的所有数据块
        while (dataQueue.TryDequeue(out BodyFaceStreamData data))
        {
            // 推送音频样本到系统音频缓冲区（低延迟）
            if (data.audio != null)
            {
                PushAudioSamples(data.audio);
            }

            // Convert and add pose frames to the animation buffer
            if (data.pose != null && data.trans != null)
            {
                int frameCount = Mathf.Min(data.pose.Count, data.trans.Count);

                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    var poseFrame = data.pose[frameIndex];
                    var transFrame = data.trans[frameIndex];
                    var blendshapeFrame = data.blendshape[frameIndex];

                    // Create rotation array
                    Quaternion[] rotations = new Quaternion[poseFrame.Count];
                    for (int i = 0; i < poseFrame.Count; i++)
                    {
                        var q = poseFrame[i];
                        rotations[i] = new Quaternion(q[0], q[1], q[2], q[3]);

                    }

                    Quaternion rotation = Quaternion.Euler(root_euler_x, root_euler_y, root_euler_z);
                    Vector3 translation = Vector3.zero;
                    if (transFrame.Count >= 3)
                    {
                        translation = new Vector3(root_p_x * transFrame[0], root_p_y * transFrame[1], root_p_z * transFrame[2]);
                        // Apply the rotation to the translation vector
                        translation = rotation * translation;
                        //translation = rotationMatrix.MultiplyPoint3x4(translation);
                    }

                    float[] blendshapeWeights = new float[blendshapeCount];
                    if (blendshapeFrame.Count >= blendshapeCount)
                    {
                        // 复制前51个blendshape权重
                        for (int i = 0; i < blendshapeCount; i++)
                        {
                            blendshapeWeights[i] = blendshapeFrame[i];
                        }

                    }

                    // Create animation frame
                    BodyFaceAnimationFrame animFrame = new BodyFaceAnimationFrame
                    {
                        rotations = rotations,
                        translation = translation / 100f,
                        blendshapeWeights = blendshapeWeights,
                    };

                    animationBuffer.Add(animFrame);
                }
            }
        }

        // 更新调试信息
        currentBufferSize = animationBuffer.Count;
        audioBufferSize = waveProvider != null ? waveProvider.BufferedBytes / 4 : 0;  // 4 bytes per float sample

        // --- 应用面部动画 ---
        if (animationBuffer.Count > motion_frame_delay)
        {
            frameTimer += Time.deltaTime;
            float frameDuration = 1.0f / FRAME_RATE;

            // 基于经过的时间应用帧
            while (frameTimer >= frameDuration && animationBuffer.Count > motion_frame_delay)
            {
                ApplyBodyFaceAnimationFrame(animationBuffer[0]);
                animationBuffer.RemoveAt(0);
                frame_index += 1;
                frameTimer -= frameDuration;
            }
        }
    }

    void ApplyBodyFaceAnimationFrame(BodyFaceAnimationFrame frame)
    {
        SkinnedMeshRenderer[] faceObjects = { faceObject1, faceObject2, faceObject3, faceObject4, faceObject5 };

        // 为每个面部对象应用blendshape权重
        for (int objIndex = 0; objIndex < 4; objIndex++)
        {
            if (faceObjects[objIndex] != null && faceObjects[objIndex].sharedMesh != null)
            {
                var mesh = faceObjects[objIndex].sharedMesh;
                int maxBlendshapes = Mathf.Min(blendshapeCount, mesh.blendShapeCount);

                // 直接按顺序应用前51个（或更少）blendshape
                for (int i = 0; i < maxBlendshapes; i++)
                {
                    float weight = frame.blendshapeWeights[i] * 100f; // 转换为0-100范围
                    faceObjects[objIndex].SetBlendShapeWeight(i, weight);
                }
            }
        }

        if (orderedBoneTransforms == null || frame.rotations.Length != orderedBoneTransforms.Length)
        {
            UnityEngine.Debug.LogWarning("Animation frame rotation data length does not match bone count.");
            return;
        }

        {
            // Apply rotations relative to initial pose
            for (int i = 0; i < orderedBoneTransforms.Length; i++)
            //for (int i = 0; i < 1; i++)
            {
                if (orderedBoneTransforms[i] != null)
                {
                    orderedBoneTransforms[i].localRotation = initialRotations[i] * frame.rotations[i];
                    root_x = frame.translation.x;
                    root_y = frame.translation.y;
                    root_z = frame.translation.z;
                }
            }
        }

        // Apply translation to root bone
        if (rootBone != null)
        {
            if (initialPoseCaptured)
            {
                // Add translation to initial position
                rootBone.localPosition = initialRootPosition + frame.translation;
                //rootBone.localPosition = frame.translation;
                //rootBone.position = frame.translation;
            }
            else
            {
                rootBone.localPosition = frame.translation;
            }
        }
    }

    void OnApplicationQuit()
    {
        StopServer();
        
        // 清理系统音频
        CleanupSystemAudio();
        
        // 关闭Python进程
        if (pythonProcess != null && !pythonProcess.HasExited)
        {
            pythonProcess.Kill();
            pythonProcess.Dispose();
            UnityEngine.Debug.Log("Python process terminated.");
        }
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));

        GUILayout.Label($"Server Running: {isServerRunning}");
        GUILayout.Label($"Client Connected: {isClientConnected}");
        GUILayout.Label($"Animation Buffer: {currentBufferSize} frames");
        GUILayout.Label($"Audio Buffer: {audioBufferSize} samples");

        // 显示面部对象状态
        GUILayout.Label($"Face Object 1: {(faceObject1 != null ? "Assigned" : "Not Assigned")}");
        GUILayout.Label($"Face Object 2: {(faceObject2 != null ? "Assigned" : "Not Assigned")}");
        GUILayout.Label($"Face Object 3: {(faceObject3 != null ? "Assigned" : "Not Assigned")}");
        GUILayout.Label($"Face Object 4: {(faceObject4 != null ? "Assigned" : "Not Assigned")}");
        GUILayout.Label($"Face Object 5: {(faceObject5 != null ? "Assigned" : "Not Assigned")}");
        GUILayout.Label($"Initial Pose Captured: {initialPoseCaptured}");
        GUILayout.Label($"Animation Buffer: {animationBuffer.Count} frames");

        if (!initialPoseCaptured && GUILayout.Button("Capture Initial Pose"))
        {
            CaptureInitialPose();
        }

        if (initialPoseCaptured && GUILayout.Button("Reset to Initial Pose"))
        {
            ResetToInitialPose();
        }
        if (!isServerRunning && GUILayout.Button("Start Server"))
        {
            StartServer();
        }

        if (isServerRunning && GUILayout.Button("Stop Server"))
        {
            StopServer();
        }

        GUILayout.EndArea();
    }
}