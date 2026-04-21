using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WorldStateDto
{
    public int tick;
    public float gameTime;
    public List<AgentDto> agents = new();
    public List<TreasureDto> treasures = new();
    public List<GolemDto> golems = new();
    public List<BaseDto> bases = new();
}

[Serializable]
public class AgentDto
{
    public string id;
    public string team;
    public Vector3 pos;
    public bool isStunned;
    public float stealCharge;
    public bool hasTreasure;
    public Vector3? destination;
}

[Serializable]
public class TreasureDto
{
    public string id;
    public Vector3 pos;
    public int value;
    public float weight;
}

[Serializable]
public class GolemDto
{
    public string id;
    public Vector3 pos;
}
[Serializable] public class ClientCommand { public string id; public string action; public Vector3 target; }
[Serializable] public class ClientCommandBatch { public List<ClientCommand> actions; }

[Serializable]
public class AgentCommand
{
    public string id;
    public string action;
    public Vector3 target;
}

[Serializable]
public class BaseDto
{
    public string team;
    public Vector3 pos;
    public float score;
}

