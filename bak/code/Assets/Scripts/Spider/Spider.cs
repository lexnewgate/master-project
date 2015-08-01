﻿using UnityEngine;
using System.Collections;

public class Spider : MonoBehaviour
{
	
	
	//objects----view all 
	public Transform plane;
	
	//spider paras
	public float speed;
	public float runSpeed;
	public float maxWalkDuration; //when spider waZlk max, it should enter into idle state.
	public float minWalkDuration; //the random walk period should be bigger than minWalk
	public float maxIdleDuration;
	public float minIdleDuration;
	public float maxSmallTurn;
	public float eyeScope;
	
	//env paras
	public float edgeWarningDist;
	private float edgeXLength;
	private float edgeYLength;
	private bool edgeTask;
	
	
	//changing randoms
	public float walkDuration;
	public float idleDuration;
	private Rigidbody rb;
	private Animator anim;
	private GameObject head;
	
	//records
	private float timeRecord;
	private Direction direction;
	private float turn;
	private Direction edgeHint;
	private Direction edgeHintbuf;
	private int  edgeHintbufCount;
	private Vector3 flyPos;
	private GameObject fly;
	private Direction turnHint;
	private float[]turningProbabilities;
	
	private Direction directionAvoid;
	private Status spiderStatus;
	
	
	private Vector3 newHeadingAfterEdgeHint;
	
	enum Direction
	{
		FORWARD,
		LEFT,
		RIGHT,
		UP,
		DOWN,
		NONE
	}
	;
	
	enum Status
	{
		FREE_WALK,
		WALK_ALONG_EDGE,
		STOP_TURN
	}
	
	
	//private Animation animation;
	
		void resetTurningProbabilities(Status status)
		{
			switch(status)
			{
			case Status.FREE_WALK:
				turningProbabilities [0] = 0.25f;//turn right
				turningProbabilities [1] = 0.25f;//turn left
				turningProbabilities [2] = 0.5f;//no turn
				break;
			case Status.WALK_ALONG_EDGE:
				switch(directionAvoid)
				{
				case Direction.LEFT:
					turningProbabilities [0] = 0.35f;//turn right
					turningProbabilities [1] = 0f;//turn left
					turningProbabilities [2] = 0.65f;//no turn
					break;
				case Direction.RIGHT:
					turningProbabilities [0] = 0f;//turn right
					turningProbabilities [1] = 0.35f;//turn left
					turningProbabilities [2] = 0.65f;//no turn
					break;
				}
				break;
	
	
			}
		}
	
	// Use this for initialization
	void Start ()
	{
		
				turningProbabilities = new float[3];
		//		turningProbabilities [0] = 0.25;//turn right
		//		turningProbabilities [1] = 0.25;//turn left
		//		turningProbabilities [2] = 0.5;//no turn
		
		anim = gameObject.GetComponent<Animator> ();
		rb = GetComponent<Rigidbody> ();
		debugSetLayerWeight ();
		setWalkGaitPattern ();
		timeRecord = 0;
		//init spider paras
		spiderStatus = Status.FREE_WALK;
		
		
		
		maxIdleDuration = 5.0f;
		minIdleDuration = 3.0f;
		minWalkDuration = 10.0f;
		maxWalkDuration = 20.0f;
		maxSmallTurn = 0.2f;
		direction = Direction.NONE;
		edgeHint = Direction.NONE;
		edgeHintbuf = Direction.NONE;
		directionAvoid = Direction.NONE;
		edgeWarningDist = 15.0f;
		eyeScope = 40.0f;
		edgeTask = false;
		
		//init env paras
		
		edgeXLength = plane.GetComponent<Renderer> ().bounds.size.x;
		edgeYLength = plane.GetComponent<Renderer> ().bounds.size.z;
		
		//		flyPos = Vector3.zero;
		//		fly = null;
		//		
		//		
		//set default state as IDLE
		anim.SetBool ("_isMoving", false);
		idleDuration = Random.Range (minIdleDuration, maxIdleDuration);
		//		direction = getRandomDirection ();
		//		speed=0;changeVelocityInXZ(rb.transform.forward);
		
	}
	
	void setDirectionAvoid(float turn)
	{
		if (turn > 0) {
			directionAvoid = Direction.LEFT;
		} else {
			directionAvoid = Direction.RIGHT;
		}
	}
	
	
	
	// Update is called once per frame
	void Update ()
	{
		Debug.Log(spiderStatus);
		updateLimbFreq ();
		
		Vector2 headingDirection = new Vector2 (rb.transform.forward.x, rb.transform.forward.z);
		//headingDirection.Normalize ();
		
		//edge turning detect
		edgeHint = edgeWarning (rb.transform.position.x, rb.transform.position.z, headingDirection,ref turnHint);
		
		/*
		 * edge turning response
		 */
		//edge hint buf: add dead zone for edge detect
		if (edgeHintbuf != edgeHint) {
			spiderStatus=Status.STOP_TURN;
			if(edgeHintbufCount<=3)
			{
				rb.angularVelocity = transform.up * turn;
				edgeHintbufCount+=1;
			}
			else
			{
				edgeHintbuf=edgeHint;
				edgeHintbufCount=0;
				//	setDirectionAvoid(turn);
			}
			
			return;
		}
		
		if (edgeHint != Direction.NONE) {
			spiderStatus=Status.STOP_TURN;

			edgeHintbuf=edgeHint;
			
			if(turnHint==Direction.RIGHT)
			{
				speed=0;
				//changeVelocityInXZ(rb.transform.forward,0);
				turn=0.4f;
			}
			else if(turnHint==Direction.LEFT)
			{
				speed=0;
				//changeVelocityInXZ(rb.transform.forward,0);
				turn=-0.4f;
			}
			rb.angularVelocity = transform.up * turn;
		} else {
			

			Direction edgeWalkDetectDir=edgeWalkDetect(rb.transform.position.x,rb.transform.position.y,
			                                           new Vector2(rb.transform.forward.x,rb.transform.forward.z),
			                                           ref directionAvoid);
			
			if(Direction.NONE==edgeWalkDetectDir)
			{
				spiderStatus=Status.FREE_WALK;
			}
			else
			{
				spiderStatus=Status.WALK_ALONG_EDGE;
			}

			resetTurningProbabilities(spiderStatus);
		
			normalResponse ();
		}    
	}
	
	Direction edgeWalkDetect(float x, float y, Vector2 headingDirection,ref Direction turnAvoid)
	{
		float [] edgeDists = new float[4];
		
		edgeDists [0] = Mathf.Abs (x + edgeXLength / 2);//left x dist
		edgeDists [1] = Mathf.Abs (x - edgeXLength / 2);//right x dist
		edgeDists [2] = Mathf.Abs (y + edgeYLength / 2);//down y dist
		edgeDists [3] = Mathf.Abs (y - edgeYLength / 2);//up y dist
		
		
		
	//	Debug.Log (edgeDists[0]+":\t"+edgeDists[1]+":\t"+edgeDists[2]+":\t"+edgeDists[3]+":\t"+edgeWarningDist);
		Direction edgeHintRet = Direction.NONE;
		
		if (edgeWarningDist+10 >= edgeDists [0] ) {
			edgeHintRet=Direction.LEFT;
			if (headingDirection.y >= 0) {
				

				turnAvoid=Direction.LEFT;
			} else {
				turnAvoid=Direction.RIGHT;

			}
		} else if (edgeWarningDist +10>= edgeDists [1] ) {
			
			edgeHintRet=Direction.RIGHT;

			if (headingDirection.y > 0) {
				turnAvoid=Direction.RIGHT;

			} else {

				turnAvoid=Direction.LEFT;
			}
		} else if (edgeWarningDist+10 >= edgeDists [2] ) {
			
			edgeHintRet=Direction.DOWN;

			if (headingDirection.x > 0) {
				turnAvoid=Direction.RIGHT;

			} else {
			
				turnAvoid=Direction.LEFT;
			}
		} else if (edgeWarningDist+10>= edgeDists [3]) {
			edgeHintRet=Direction.UP;
			if (headingDirection.x > 0) {

				turnAvoid=Direction.LEFT;
			} else {
				turnAvoid=Direction.RIGHT;

			}
		}
		
		return edgeHintRet;
	}
	
	
	
	
	
	Vector3 getNewHeadingAfterEdgeHint(Direction edgeHint)
	{
		Vector3 randomVec= getRandomVector();
		switch(edgeHint)
		{
		case Direction.LEFT:
			if(randomVec.x<0)
				randomVec.Set(-randomVec.x,randomVec.y,randomVec.z);
			break;
		case Direction.RIGHT:
			if(randomVec.x>0)
				randomVec.Set(-randomVec.x,randomVec.y,randomVec.z);
			break;
		case Direction.DOWN:
			if(randomVec.z<0)
				randomVec.Set(randomVec.x,randomVec.y,-randomVec.z);
			break;
		case Direction.UP:
			if(randomVec.z>0)
				randomVec.Set(-randomVec.x,randomVec.y,-randomVec.z);
			break;
			
		}
		
		return randomVec;
	}
	
	void updateLimbFreq()
	{
		
		
		if (anim.GetBool ("_isMoving") && speed > 0) {
			anim.SetFloat ("_speed", 0.3f);
			anim.SetFloat ("_turn", turn);
		} else if (anim.GetBool ("_isMoving") && 0==speed) {
			anim.SetFloat ("_speed", 0f);
			anim.SetFloat ("_turn", turn);
		}
		else {
			
			anim.SetFloat ("_turn", turn);
		}
	}
	
	//walk and idle
	void normalResponse()
	{
		timeRecord += Time.deltaTime;
		if (!anim.GetBool ("_isMoving")) {
			//idle state
			if (timeRecord > idleDuration) {
				/*
						 * set changing state
						 */
				timeRecord = 0;
				anim.SetBool ("_isMoving", true);
				walkDuration = Random.Range (minWalkDuration, maxWalkDuration);
				direction = getRandomDirection ();
			}
			
		} else if (anim.GetBool ("_isMoving")) {
			//moving state
			speed=5;
			changeVelocityInXZ(rb.transform.forward,5.0f);
			switch(direction)
			{
			case Direction.FORWARD:
				turn=0;
				break;
			case Direction.RIGHT:
				turn=0.3f;
				break;
			case Direction.LEFT:
				turn=-0.3f;
				break;
				
			}
			rb.angularVelocity = transform.up * turn;
			if (timeRecord > walkDuration) {
				//set changing state
				timeRecord = 0;
				anim.SetBool ("_isMoving", false);
				direction=Direction.FORWARD;
				idleDuration = Random.Range (minIdleDuration, maxIdleDuration);
				//rb.velocity=Vector3.zero;
				
			}
			
			
		}
		
		
		
		//handle velocity
		//		if (anim.GetBool ("_isMoving")) {
		
		//			
		//			
		//		}
	}
	
	
	void resetRunTowards()
	{
		speed = 10.0f;
	}
	void runTowards(Vector3 pos)
	{
		resetRunTowards ();
		Vector3 targetDir = pos-transform.position;
		targetDir.y=0;
		Vector3 newDir = Vector3.RotateTowards(transform.forward, targetDir, 1.0f, 0.0F);
		transform.rotation=Quaternion.LookRotation(newDir);
		//changeVelocityInXZ(pos-transform.position);
		anim.SetBool ("_isMoving", true);
	}
	
	void changeVelocityInXZ(Vector3 directionInXZ,float speed)
	{
		
		Vector3 velocity = directionInXZ;
		velocity.Normalize ();
		velocity = velocity * speed;
		velocity.y = rb.velocity.y;
		rb.velocity = velocity;
	}
	
	void OnTriggerEnter(Collider other) {
		//Destroy(other.gameObject);
		if (other.gameObject.CompareTag ("fly")) {
			Destroy(other.gameObject);
			UI.score+=1;
		}
	}
	
	bool EyeRays(ref Vector3 targetPos)
	{
		Vector3 defaultV = transform.forward;
		Vector3 [] eyeRays=new Vector3[7];
		eyeRays[0]=defaultV;
		eyeRays[1]= Quaternion.AngleAxis (30, transform.up)*defaultV;
		eyeRays[2]= Quaternion.AngleAxis (-30, transform.up)*defaultV;
		//eyeRays[3]= Quaternion.AngleAxis (15, transform.right)*defaultV;
		//eyeRays[4]= Quaternion.AngleAxis (-15, transform.right)*defaultV;
		eyeRays[3]= Quaternion.AngleAxis (45, transform.up)*defaultV;
		eyeRays[4]= Quaternion.AngleAxis (-45, transform.up)*defaultV;
		eyeRays[5]= Quaternion.AngleAxis (15, transform.up)*defaultV;
		eyeRays[6]= Quaternion.AngleAxis (-15, transform.up)*defaultV;
		
		for (int i=0; i<eyeRays.Length; i++) {
			Debug.DrawRay (transform.position,eyeRays[i]*eyeScope,Color.green,0,false);		
		}
		
		RaycastHit hit;
		
		
		
		for (int i=0; i<eyeRays.Length; i++) {
			if (Physics.Raycast (transform.position, eyeRays[i], out hit, eyeScope)) {
				if(hit.collider.gameObject.tag=="fly")
				{
					targetPos=hit.collider.gameObject.transform.position;
					fly=hit.collider.gameObject;
					return true;
				}
			}
			
		}
		
		return false;
	}
	
	
	
	
	
	
	
	//	void edgeResponse()
	//	{
	//		//the spider's heading direction
	//		Vector2 headingDirection = new Vector2 (rb.velocity.x, rb.velocity.z);
	//		headingDirection.Normalize ();
	//		edgeHint = edgeWarning (transform.position.x, transform.position.z, headingDirection);
	//		if(edgeHint!=Direction.NONE)
	//		{
	//			Vector3 randomVec= getRandomVector();
	//			Vector3 newDir=Vector3.zero;
	//			switch(edgeHint)
	//			{
	//			case Direction.LEFT:
	//				if(randomVec.x<0)
	//					randomVec.Set(-randomVec.x,randomVec.y,randomVec.z);
	//				newDir=Vector3.RotateTowards(transform.forward,randomVec,1.0f,0);
	//				break;
	//			case Direction.RIGHT:
	//				if(randomVec.x>0)
	//					randomVec.Set(-randomVec.x,randomVec.y,randomVec.z);
	//				newDir=Vector3.RotateTowards(transform.forward,randomVec,1.0f,0);
	//				break;
	//			case Direction.DOWN:
	//				if(randomVec.z<0)
	//					randomVec.Set(randomVec.x,randomVec.y,-randomVec.z);
	//				newDir=Vector3.RotateTowards(transform.forward,randomVec,1.0f,0);
	//				break;
	//			case Direction.UP:
	//				if(randomVec.z>0)
	//					randomVec.Set(-randomVec.x,randomVec.y,-randomVec.z);
	//				newDir=Vector3.RotateTowards(transform.forward,randomVec,1.0f,0);
	//				break;
	//				
	//			}
	//			
	//			transform.rotation=Quaternion.LookRotation(newDir);
	//		}
	//	}
	
	
	
	/*
			 * The edgeWarning will be triggered when A&&B
			 * Condition A:
			 * the spider are too close to the edge
			 * Condition B:
			 * the spider are heading towards to the closest edge
			 * 
			 * Note:
			 * if the spider are very close to one edge but heading reverse
			 * direction, then the function won't be triggered.
		 	*/
	Direction edgeWarning (float x, float y, Vector2 headingDirection,ref Direction turnHint)
	{
		
		/*
				 * has to calc the heading direction
					*/
		float [] edgeDists = new float[4];
		
		edgeDists [0] = Mathf.Abs (x + edgeXLength / 2);//left x dist
		edgeDists [1] = Mathf.Abs (x - edgeXLength / 2);//right x dist
		edgeDists [2] = Mathf.Abs (y + edgeYLength / 2);//down y dist
		edgeDists [3] = Mathf.Abs (y - edgeYLength / 2);//up y dist
		
		//	Debug.Log ("warning:"+edgeDists[0]+":\t"+edgeDists[1]+":\t"+edgeDists[2]+":\t"+edgeDists[3]+":\t"+edgeWarningDist);
		
		
		
		
		Direction edgeHintRet = Direction.NONE;
		
		if (edgeWarningDist >= edgeDists [0] && headingDirection.x < 0) {
			edgeHintRet=Direction.LEFT;
			newHeadingAfterEdgeHint=getNewHeadingAfterEdgeHint(edgeHint);
			if (headingDirection.y >= 0) {
				
				turnHint=Direction.RIGHT;
				
			} else {
				
				turnHint=Direction.LEFT;
			}
		} else if (edgeWarningDist >= edgeDists [1] && headingDirection.x > 0) {
			
			edgeHintRet=Direction.RIGHT;
			newHeadingAfterEdgeHint=getNewHeadingAfterEdgeHint(edgeHint);
			if (headingDirection.y > 0) {
				
				//edgeHintRet = -1;
				turnHint=Direction.LEFT;
			} else {
				turnHint=Direction.RIGHT;
				
			}
		} else if (edgeWarningDist >= edgeDists [2] && headingDirection.y < 0) {
			
			edgeHintRet=Direction.DOWN;
			newHeadingAfterEdgeHint=getNewHeadingAfterEdgeHint(edgeHint);
			if (headingDirection.x > 0) {
				//edgeHintRet = -1;
				turnHint=Direction.LEFT;
			} else {
				turnHint=Direction.RIGHT;
				
			}
		} else if (edgeWarningDist >= edgeDists [3] && headingDirection.y > 0) {
			edgeHintRet=Direction.UP;
			newHeadingAfterEdgeHint=getNewHeadingAfterEdgeHint(edgeHint);
			if (headingDirection.x > 0) {
				turnHint=Direction.RIGHT;
				
			} else {
				//edgeHintRet = -1;
				turnHint=Direction.LEFT;
			}
		}
		
		return edgeHintRet;
		
	}
	
	/*
			 * the spider random direction:
			 * probability allocation:
			 * 0.15 for turning left
			 * 0.15 for turning right
			 * 0.7 for forward
			 * 
			 * The random dirction will only function when no 
			 * high priority actions triggered:
			 * such as too close to edge.
			 * 
		 	*/
	Direction getRandomDirection ()
	{
		Direction ret = Direction.NONE;
		float random = Random.Range (0, 1.0f);
		if (random < turningProbabilities[0]) {
			ret = Direction.RIGHT;
		} else if (random > turningProbabilities[0] 
		           && random < turningProbabilities[0]+turningProbabilities[1]) {
			ret = Direction.LEFT;
		} else {
			ret = Direction.FORWARD;
		}
		return ret;
	}
	
	
	Vector3 getRandomVector()
	{
		float x = Random.Range (-1.0f, 1.0f);
		float z= Random.Range (-1.0f, 1.0f);
		Vector3 ret = new Vector3 (x,0, z);
		ret.Normalize ();
		return ret;
	}
	
	/*
			 * set timings of different limbs
		 	*/
	void setWalkGaitPattern ()
	{
		
		//first R1-R4 then L1-L4
		float [] timing = new float[8];
		timing [0] = -0.3f;   //R1----------4
		timing [1] = -0.7f;   //R2----------7
		timing [2] = -0.1f;   //R3----------2
		timing [3] = -0.5f;   //R4----------5
		timing [4] = -0.8f;   //L1----------8
		timing [5] = -0.2f;   //L2----------3
		timing [6] = -0.6f;   //L3----------6
		timing [7] = 0;       //L4----------1
		
		//fixed, don't change
		//change upwards to achieve effects
		float offset = 0.5f;
		anim.SetFloat ("L4_sf_offset", getFractionPart (timing [7] + offset));//1
		anim.SetFloat ("R3_sf_offset", getFractionPart (timing [2] + offset));//2
		anim.SetFloat ("L2_sf_offset", getFractionPart (timing [5] + offset));//3
		anim.SetFloat ("R1_sf_offset", getFractionPart (timing [0] + offset));//4
		anim.SetFloat ("R4_sf_offset", getFractionPart (timing [3] + offset));//5
		anim.SetFloat ("L3_sf_offset", getFractionPart (timing [6] + offset));//6
		anim.SetFloat ("R2_sf_offset", getFractionPart (timing [1] + offset));//7
		anim.SetFloat ("L1_sf_offset", getFractionPart (timing [4] + offset));//8
		
	}
	
	/*
			 * assisting functions 
			*/
	//only suitable for cases:
	//get .xxx from 1.xxx 
	//or get 1-0.xxx from -0.xxx
	float getFractionPart (float num)
	{
		float ret;
		if (num > 1) {
			ret = num - 1;
		} else if (num < 0) {
			ret = num + 1;
		} else {
			ret = num;
		}
		return ret;
	}
	
	
	
	
	/* following code for debug purposes
			*/
	
	/*
			 * this function is meant to disable or enable part of 
			 * layers to see animations of particular layers
		 	*/
	void debugSetLayerWeight ()
	{
		int L1index = anim.GetLayerIndex ("L1");
		int L2index = anim.GetLayerIndex ("L2");
		int L3index = anim.GetLayerIndex ("L3");
		int L4index = anim.GetLayerIndex ("L4");
		int R1index = anim.GetLayerIndex ("R1");
		int R2index = anim.GetLayerIndex ("R2");
		int R3index = anim.GetLayerIndex ("R3");
		int R4index = anim.GetLayerIndex ("R4");
		
		//set all layers weight to 0
		//int layerNum = 9;
		for (int i=0; i<9; i++) {
			anim.SetLayerWeight (i, 0);
		}
		
		//enable certain layers
		
		//enable L4-R3-L2-R1
		anim.SetLayerWeight (L4index, 1);
		anim.SetLayerWeight (R3index, 1);
		anim.SetLayerWeight (L2index, 1);
		anim.SetLayerWeight (R1index, 1);
		anim.SetLayerWeight (R4index, 1);
		//enable R4-L3-R2-L1
		anim.SetLayerWeight (R4index, 1);
		anim.SetLayerWeight (L3index, 1);
		anim.SetLayerWeight (R2index, 1);
		anim.SetLayerWeight (L1index, 1);
		
	}
}