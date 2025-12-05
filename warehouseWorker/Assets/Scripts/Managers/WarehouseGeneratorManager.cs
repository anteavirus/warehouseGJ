using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WarehouseGeneratorManager : GenericManager<WarehouseGeneratorManager>
{
    [System.Serializable]
    public class WarehouseLayout
    {
        public GameObject[,] room; // okay so. Vector2 of rooms basically. honestly it'd be great if we could also go into negatives so that we wouldn't have to fuck with this too much.
        // maybe a method to offset? TODO: do the do


    }
    
    int levelSeed = 0;


    public WarehouseGeneratorManager(int levelSeed)
    {
        this.levelSeed = levelSeed == 0 ? Random.Range(0, 1999) : levelSeed;
    }

    public override void Initialize()
    {
        // Use the level seed as system random and "generate" shit (i.e. just throw together prefabs while staying consistent I suppose

    }

}
