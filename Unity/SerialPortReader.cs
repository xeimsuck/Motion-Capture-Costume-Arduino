using System;
using System.Collections.Concurrent;
using System.IO.Ports;
using System.Threading;
using UnityEngine;
using TMPro;

public class SerialPortReader : MonoBehaviour
{
    [Header("Serial Settings")]
    public string portName = "COM5";
    public int baudRate = 9600;

    [Header("UI")]
    public TextMeshProUGUI[] sensorTexts = new TextMeshProUGUI[8];

    private SerialPort serialPort;
    private Thread serialThread;
    private bool isRunning;

    private ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();

    public SensorData[] sensors = new SensorData[8];

    void Start()
    {
        for (int i = 0; i < sensors.Length; i++)
            sensors[i] = new SensorData();

        try
        {
            Debug.Log("Start Serial Port");
            serialPort = new SerialPort(portName, baudRate);
            serialPort.ReadTimeout = 100;
            serialPort.Open();

            isRunning = true;
            serialThread = new Thread(ReadSerial);
            serialThread.Start();
        }
        catch (Exception e)
        {
            Debug.LogError("Ошибка открытия порта: " + e.Message);
        }
    }

    void ReadSerial()
    {
        while (isRunning)
        {
            try
            {
                string line = serialPort.ReadLine();
                Debug.Log(line);
                if (!string.IsNullOrWhiteSpace(line))
                    messageQueue.Enqueue(line.Trim());
            }
            catch (TimeoutException) { }
            catch (Exception e)
            {
                Debug.LogError("Ошибка чтения: " + e.Message);
            }
        }
    }

    void Update()
    {
        while (messageQueue.TryDequeue(out string message))
        {
            ParseMessage(message);
        }

        for (int i = 0; i < sensors.Length; i++)
        {
            if (sensorTexts[i] != null)
            {
                sensorTexts[i].text =
                    $"Sensor {i}\n" +
                    $"Acc: {sensors[i].accX:F2}, {sensors[i].accY:F2}, {sensors[i].accZ:F2}\n" +
                    $"Rot: {sensors[i].rotX:F2}, {sensors[i].rotY:F2}, {sensors[i].rotZ:F2}";
            }
        }
    }

    void ParseMessage(string msg)
    {
        string[] data = msg.Split(' ');
        if (data.Length != 7)
        {
            Debug.LogWarning($"Строка неправильного формата: {msg}");
            return;
        }

        if (!int.TryParse(data[0], out int sensorIndex) || sensorIndex < 0 || sensorIndex >= sensors.Length) {
            Debug.LogWarning($"Неверный индекс датчика: {msg}");
            return;
        }

        try
        {
            sensors[sensorIndex].accX = float.Parse(data[1], System.Globalization.CultureInfo.InvariantCulture);
            sensors[sensorIndex].accY = float.Parse(data[2], System.Globalization.CultureInfo.InvariantCulture);
            sensors[sensorIndex].accZ = float.Parse(data[3], System.Globalization.CultureInfo.InvariantCulture);
            sensors[sensorIndex].rotX = float.Parse(data[4], System.Globalization.CultureInfo.InvariantCulture);
            sensors[sensorIndex].rotY = float.Parse(data[5], System.Globalization.CultureInfo.InvariantCulture);
            sensors[sensorIndex].rotZ = float.Parse(data[6], System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            Debug.LogWarning($"Не удалось распарсить строку: {msg}");
        }
    }

    void OnApplicationQuit()
    {
        isRunning = false;
        if (serialThread != null && serialThread.IsAlive)
            serialThread.Join();

        if (serialPort != null && serialPort.IsOpen)
            serialPort.Close();
    }

    public class SensorData
    {
        public float accX, accY, accZ;
        public float rotX, rotY, rotZ;
    }
}
