using System;
using UnityEngine;

/// <summary>
/// Stage 2 stand-in for the dungeon: one physical room that is "re-rolled" every time the
/// player walks through an open door. Stage 3 replaces the random exits with a real layout.
/// </summary>
public class RoomController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DoorController[] doors = new DoorController[4]; // index = (int)DoorDirection
    [SerializeField] private QuestionManager questions;
    [SerializeField] private PlayerController player;
    [SerializeField] private HealthSystem playerHealth;

    [Header("Room rules")]
    [SerializeField, Range(0f, 1f)] private float extraDoorChance = 0.6f;
    [SerializeField] private int damageOnWrongAnswer = 1;
    [SerializeField] private float arrivalOffsetX = 8.2f;
    [SerializeField] private float arrivalOffsetY = 3.4f;

    [Header("Radio")]
    [SerializeField] private int radioRoomNumber = 2;

    private readonly bool[] hasExit = new bool[4];
    private GameObject radioPickup;

    public int RoomsVisited { get; private set; }
    public Vector2Int GridPosition { get; private set; }

    public event Action<int> RoomEntered;

    private void Start()
    {
        if (!ReferencesValid()) { enabled = false; return; }

        foreach (var door in doors) door.PlayerEntered += HandleDoorEntered;
        questions.AnswerSubmitted += HandleAnswer;
        playerHealth.Died += HandlePlayerDied;

        // 70% chance to begin the run with the radio (always possible to find it later).
        if (RadioController.Instance != null) RadioController.Instance.RollForNewRun();

        EnterRoom(null);
    }

    private void OnDestroy()
    {
        if (doors != null)
            foreach (var door in doors)
                if (door != null) door.PlayerEntered -= HandleDoorEntered;

        if (questions != null) questions.AnswerSubmitted -= HandleAnswer;
        if (playerHealth != null) playerHealth.Died -= HandlePlayerDied;
    }

    private bool ReferencesValid()
    {
        if (questions == null || player == null || playerHealth == null || doors == null || doors.Length != 4)
        {
            Debug.LogError("RoomController is missing references. Run STATBOUND > Setup Stage 2.", this);
            return false;
        }

        foreach (var door in doors)
        {
            if (door == null)
            {
                Debug.LogError("RoomController: a door reference is empty.", this);
                return false;
            }
        }

        return true;
    }

    private void EnterRoom(DoorDirection? arrivedThrough)
    {
        RoomsVisited++;

        for (int i = 0; i < 4; i++)
            hasExit[i] = arrivedThrough.HasValue && (int)arrivedThrough.Value == i
                         || UnityEngine.Random.value < extraDoorChance;

        // Every room has at least two exits so the player can never be trapped.
        int count = 0;
        foreach (bool b in hasExit) if (b) count++;
        while (count < 2)
        {
            int i = UnityEngine.Random.Range(0, 4);
            if (hasExit[i]) continue;
            hasExit[i] = true;
            count++;
        }

        for (int i = 0; i < 4; i++)
            doors[i].SetState(hasExit[i] ? DoorController.DoorState.Locked : DoorController.DoorState.Sealed);

        SpawnRadioIfNeeded();

        int difficulty = Mathf.Clamp(1 + (RoomsVisited - 1) / 3, 1, 3);
        RoomEntered?.Invoke(RoomsVisited);
        questions.AskNewQuestion(difficulty);
    }

    /// <summary>Until the player owns the radio, it lies in every room from radioRoomNumber on.</summary>
    private void SpawnRadioIfNeeded()
    {
        if (radioPickup != null) Destroy(radioPickup);

        var radio = RadioController.Instance;
        if (radio == null || radio.Owned || RoomsVisited < radioRoomNumber) return;

        var position = new Vector2(UnityEngine.Random.Range(-5f, 5f), UnityEngine.Random.Range(-2f, 2f));
        radioPickup = RadioPickup.Spawn(position);
        GameMenus.ShowToast(Loc.T("В тази стая има РАДИО! Вземи го, за да слушаш музиката си.",
            "There is a RADIO in this room! Pick it up to listen to your music."), 4f);
    }

    private void HandleAnswer(bool correct, int index, StatQuestion question)
    {
        if (correct)
        {
            AudioManager.Play(Sfx.DoorOpen);
            for (int i = 0; i < 4; i++)
                if (hasExit[i]) doors[i].SetState(DoorController.DoorState.Open);
        }
        else
        {
            playerHealth.TakeDamage(damageOnWrongAnswer);
        }
    }

    private void HandleDoorEntered(DoorController door)
    {
        if (door.State != DoorController.DoorState.Open || playerHealth.IsDead) return;

        DoorDirection travel = door.Direction;
        GridPosition += travel.ToGrid();

        player.Teleport(ArrivalPosition(travel.Opposite()));
        EnterRoom(travel.Opposite());
    }

    private Vector2 ArrivalPosition(DoorDirection arrivalDoor)
    {
        switch (arrivalDoor)
        {
            case DoorDirection.North: return new Vector2(0f, arrivalOffsetY);
            case DoorDirection.South: return new Vector2(0f, -arrivalOffsetY);
            case DoorDirection.East: return new Vector2(arrivalOffsetX, 0f);
            default: return new Vector2(-arrivalOffsetX, 0f);
        }
    }

    private void HandlePlayerDied()
    {
        player.InputLocked = true;
    }
}
