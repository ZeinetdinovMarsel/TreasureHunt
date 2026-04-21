using System;
using System.Collections.Generic;
using UnityEngine;
[Serializable]
public struct Vector3Dto
{
    public float x;
    public float y;
    public float z;

    public Vector3Dto(Vector3 v)
    {
        x = v.x;
        y = v.y;
        z = v.z;
    }

    public Vector3 GetVector()
    {
        return new Vector3(x, y, z) ;
    }
}
[Serializable]
public class WorldStateDto
{
    public int tick;
    public float gameTime;
    public string gameState;
    public List<BaseDto> bases = new();
    public List<AgentDto> agents = new();
    public List<GolemDto> golems = new();
    public List<TreasureDto> treasures = new();
}

[Serializable]
public class BaseDto
{
    public string team;
    public Vector3Dto pos;
    public float points;
}

[Serializable]
public class AgentDto
{
    public int agentId;
    public string team;
    public Vector3Dto pos;
    public float weight;
    public float currentSpeed;
    public bool isStunned;
    public bool hasTreasure;
    public string heldTreasureId;
    public bool stealAbilityReady;
    public float stealChargePercentage;
    public Vector3Dto? destination;
}

[Serializable]
public class GolemDto
{
    public string id;
    public Vector3Dto pos;
    public float currentSpeed;
    public string state;
    public int? targetAgentId;
}

[Serializable]
public class TreasureDto
{
    public string id;
    public Vector3Dto pos;
    public bool isPicked;
    public int? holderAgentId;
    public int value;
    public float weight;
}
[Serializable] public class ClientCommand { public string id; public string action; public Vector3Dto target; public string team; }
[Serializable] public class ClientCommandBatch { public List<ClientCommand> actions; }

[Serializable]
public class AgentCommand
{
    public string id;
    public string action;
    public Vector3Dto target;
}

[Serializable]
public class JoinRequest
{
    public string playerId;
    public string team;
    public bool ready;
}