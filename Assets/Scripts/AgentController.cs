﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AgentController : MonoBehaviour
{
    public GameObject home;
    private EconomyManager econManager;

    private GameObject target = null;
    private int mode; //0 for wood, 1 for fruit
    private GameObject heldObject = null;

    public float walkSpeed = 15.0f;
    public float turnSpeed = 15.0f;
    public float harvestDistance = 3.0f;
    public float deliverDistance = 0.1f;
    public int numTreesHarvested = 0;
    public int numMangoesHarvested = 0;

    //temporary fixed array of time ratios
    private const int numRatios = 11;
    private float[] ratios = new float[numRatios];
    public float woodCollectionTimeRatio;


    public float collectionTime = 10.0f; //seconds
    //public float accelerationFactor = 1.0f; //For speeding up sims later on.
    private float startTime;
    public bool dayOver = true;

    public List<AgentDay> activityLog = new List<AgentDay>();

    void Awake()
    {
      //I'm certain there's a preferred way to do this,
      //though it doesn't seem preferable to have to assign it in the UI.
      //GameObject econManagerObject = GameObject.Find("EconomyManager");
      econManager = GameObject.Find("EconomyManager").GetComponent<EconomyManager>();

      //temporary fixed array of time ratios
      for (int i = 0; i < numRatios; i++)
      {
        ratios[i] = (float)i / (float)(numRatios - 1);
      }
      Debug.Log(ratios);
    }

    // Update is called once per frame
    void Update()
    {
      if (Time.time - startTime > collectionTime)
      {
        dayOver = true;
      }
    }

    void DetermineTimeAllocation(int date)
    {
      //TODO: choose based on previous outcomes and utility function
      woodCollectionTimeRatio = ratios[date % (numRatios - 1)];
      Debug.Log(date);
      Debug.Log(woodCollectionTimeRatio);
    }

    public void StartWorkDay(int date)
    {
      DetermineTimeAllocation(date);
      activityLog.Add(new AgentDay(date, woodCollectionTimeRatio));

      startTime = Time.time;
      dayOver = false;
      StartCoroutine("GoHarvest");
    }

    IEnumerator GoHarvest()
    {
      //Check time allocation
      if (Time.time - startTime < woodCollectionTimeRatio * collectionTime)
      {
        mode = 0; //Collect wood
      }
      else
      {
        mode = 1; //Collect fruit
      }

      //Pick target
      if (target == null)
      {
        switch(mode)
        {
          case 0:
            target = FindClosestHarvestableWithTag("wood");
            numTreesHarvested++;
            break;
          case 1:
            target = FindClosestHarvestableWithTag("fruit");
            numMangoesHarvested++;
            break;
        }
        //TODO: Handle the case where no more potential targets remain
      }

      while (GoToTargetIfNotThere(harvestDistance))
      {
        yield return null;
      }
      //Harvest
      heldObject = target.GetComponent<HarvestableController>().HandleHarvest();
      heldObject.transform.parent = gameObject.transform;
      //TODO: Animate movement of heldObject

      StartCoroutine("GoDeliver");
    }

    IEnumerator GoDeliver()
    {
      target = home;
      while (GoToTargetIfNotThere(deliverDistance))
      {
        yield return null;
      }
      if (heldObject == null) {
        Debug.LogWarning("Agent is trying to deliver null in GoDeliver()");
      }
      heldObject.transform.parent = null;
      heldObject = null;
      target = null;
      //TODO: Animate movement of heldObject

      if (dayOver)
      {
        StartCoroutine("GoHome");
      }
      else
      {
        StartCoroutine("GoHarvest");
      }
    }

    IEnumerator GoHome()
    {
      //Keeping this separate from GoDeliver since I'm planning to have separate
      //home and storage area objects.
      target = home;
      while (GoToTargetIfNotThere())
      {
        yield return null;
      }

      //Log day's outcome
      activityLog.Last().numTreesHarvested = numTreesHarvested;
      activityLog.Last().numMangoesHarvested = numMangoesHarvested;

      //Reset state
      target = null;
      mode = 0;
      numTreesHarvested = 0;
      numMangoesHarvested = 0;

      //Tell manager we're done
      econManager.AgentIsDone(gameObject);
    }

    private bool GoToTargetIfNotThere(float goalDistance = 0)
    {
      //This seems like it might break some best practices
      Vector3 heading = target.transform.position - transform.position;
      heading = new Vector3 (heading.x, 0, heading.z); //project to xz plane
      float distance = heading.magnitude;
      if (distance > goalDistance)
      {
          Vector3 groundTarget = new Vector3(
            target.transform.position.x,
            0,
            target.transform.position.z
          );

          //Translate
          gameObject.transform.position = Vector3.MoveTowards(
            gameObject.transform.position,
            groundTarget,
            Time.deltaTime * walkSpeed
          );

          //Rotate
          Vector3 targetDir = groundTarget - transform.position;
          float step = turnSpeed * Time.deltaTime;
          Vector3 newDir = Vector3.RotateTowards(transform.forward, targetDir, step, 0.0f);
          transform.rotation = Quaternion.LookRotation(newDir);

          return true;
      }
      else
      {
        return false;
      }
    }

    public GameObject FindClosestHarvestableWithTag(string tag)
    {
        GameObject[] goods;
        goods = GameObject.FindGameObjectsWithTag(tag);

        GameObject closest = null;
        float distance = Mathf.Infinity;
        Vector3 position = transform.position;
        foreach (GameObject go in goods)
        {
            Vector3 diff = go.transform.position - position;
            float curDistance = diff.sqrMagnitude;
            if (curDistance < distance)
            {
                if (go.GetComponent<HarvestableController>().harvested == false)
                {
                  closest = go;
                  distance = curDistance;
                }
            }
        }
        return closest;
    }
}
