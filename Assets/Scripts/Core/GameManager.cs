using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class GameManager : MonoBehaviour
{
    /*
    * GameManager is responsible for 
    * : start, pause, resume, reset, end the simulation.
    * : manage the simulation speed (simulationSpeed) and the simulation time (SimulationTime).
    * : manage the game states [Initializing, Running, Paused, GameOver].
    * : manage the event OnStateChanged(GameState).
    */
    public static GameManager Instance { get; private set; }

    public enum GameState
    {
        Initializing,
        Running,
        Paused,
        GameOver
    }

    [Header("References")]
    [SerializeField] private Transform xrOrigin;
    [SerializeField] private Transform factoryMachine;

    [Header("Simulation Settings")]
    [SerializeField] private float simulationSpeed = 1f;
    [SerializeField] private bool startOnAwake = true;

    public GameState CurrentState { get; private set; } = GameState.Initializing;
    public float SimulationTime { get; private set; }

    public event Action<GameState> OnStateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // Initialize the game
        SimulationTime = 0f;
        if (xrOrigin == null)
        {
            var origin = FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
            if (origin != null)
                xrOrigin = origin.transform;
        }
        SetState(GameState.Initializing);
        Debug.Log("[GameManager] Initialized");

        if (startOnAwake) 
        {
            StartSimulation();
        }
    }

    private void Update()
    {
        if (CurrentState != GameState.Running)
            return;

        SimulationTime += Time.deltaTime * simulationSpeed;
    }

    public void StartSimulation()
    {
        SetState(GameState.Running);
        Debug.Log("[GameManager] Simulation started");
    }

    public void PauseSimulation()
    {
        if (CurrentState != GameState.Running) return;

        SetState(GameState.Paused);
        Time.timeScale = 0f;
        Debug.Log("[GameManager] Simulation paused");
    }

    public void ResumeSimulation()
    {
        if (CurrentState != GameState.Paused) return;

        Time.timeScale = 1f;
        SetState(GameState.Running);
        Debug.Log("[GameManager] Simulation resumed");
    }

    public void ResetSimulation()
    {
        Time.timeScale = 1f;
        SimulationTime = 0f;

        SetState(GameState.Initializing);
        Debug.Log("[GameManager] Simulation reset");
    }

    public void EndSimulation()
    {
        SetState(GameState.GameOver);
        Debug.Log($"[GameManager] Simulation ended — Time: {SimulationTime:F1}s");
    }

    public void SetSimulationSpeed(float speed)
    {
        simulationSpeed = Mathf.Max(0f, speed);
    }

    private void SetState(GameState newState)
    {
        if (CurrentState == newState) return;

        CurrentState = newState;
        OnStateChanged?.Invoke(newState);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
