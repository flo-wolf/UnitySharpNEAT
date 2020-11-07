/*
------------------------------------------------------------------
  This file is part of UnitySharpNEAT 
  Copyright 2020, Florian Wolf
  https://github.com/flo-wolf/UnitySharpNEAT
------------------------------------------------------------------
*/
using UnityEngine;
using System.Collections;
using SharpNeat.Phenomes;

namespace UnitySharpNEAT
{
    /// <summary>
    /// The CarController controls a car (Unit) that learns to drive around a racetrack.
    /// Five distance sensors measure its distance to walls, which get input into its Neural Network. 
    /// The Output corresponds to steering and thrust control. 
    /// The fitness of this Unit is calculated by the amount of road piceces it passes, how many laps it drove and how (few) wall hits it has taken.
    /// When the Unit gets deactivated, it gets hidden and its values including its Transform get reset, to get a "fresh", reusable Unit on its next Activation.
    /// The resetting is important, since we use object pooling to resuse Units instead of instantiating/destroying them.
    /// </summary>
    public class CarController : UnitController
    {
        // general control variables
        public float Speed = 5f;
        public float TurnSpeed = 180f;
        public float SensorRange = 10;

        // track progress
        public int Lap = 1;
        public int CurrentPiece = 0;
        public int LastPiece = 0;
        public int WallHits = 0;

        private bool _movingForward = true;

        // cache the initial transform of this unit, to reset it on deactivation
        private Vector3 _initialPosition = default;
        private Quaternion _initialRotation = default;

        private void Start()
        {
            // cache the inital transform of this Unit, so that when the Unit gets reset, it gets put into its initial state
            _initialPosition = transform.position;
            _initialRotation = transform.rotation;
        }

        protected override void UpdateBlackBoxInputs(ISignalArray inputSignalArray)
        {
            float frontSensor = 0;
            float leftFrontSensor = 0;
            float leftSensor = 0;
            float rightFrontSensor = 0;
            float rightSensor = 0;

            // Five raycasts into different directions each measure how far a wall is away.
            RaycastHit hit;
            if (Physics.Raycast(transform.position + transform.forward * 1.1f, transform.TransformDirection(new Vector3(0, 0, 1).normalized), out hit, SensorRange))
            {
                if (hit.collider.CompareTag("Wall"))
                {
                    frontSensor = 1 - hit.distance / SensorRange;
                }
            }

            if (Physics.Raycast(transform.position + transform.forward * 1.1f, transform.TransformDirection(new Vector3(0.5f, 0, 1).normalized), out hit, SensorRange))
            {
                if (hit.collider.CompareTag("Wall"))
                {
                    rightFrontSensor = 1 - hit.distance / SensorRange;
                }
            }

            if (Physics.Raycast(transform.position + transform.forward * 1.1f, transform.TransformDirection(new Vector3(1, 0, 0).normalized), out hit, SensorRange))
            {
                if (hit.collider.CompareTag("Wall"))
                {
                    rightSensor = 1 - hit.distance / SensorRange;
                }
            }

            if (Physics.Raycast(transform.position + transform.forward * 1.1f, transform.TransformDirection(new Vector3(-0.5f, 0, 1).normalized), out hit, SensorRange))
            {
                if (hit.collider.CompareTag("Wall"))
                {
                    leftFrontSensor = 1 - hit.distance / SensorRange;
                }
            }

            if (Physics.Raycast(transform.position + transform.forward * 1.1f, transform.TransformDirection(new Vector3(-1, 0, 0).normalized), out hit, SensorRange))
            {
                if (hit.collider.CompareTag("Wall"))
                {
                    leftSensor = 1 - hit.distance / SensorRange;
                }
            }

            // modify the ISignalArray object of the blackbox that was passed into this function, by filling it with the sensor information.
            // Make sure that NeatSupervisor.NetworkInputCount fits the amount of sensors you have
            inputSignalArray[0] = frontSensor;
            inputSignalArray[1] = leftFrontSensor;
            inputSignalArray[2] = leftSensor;
            inputSignalArray[3] = rightFrontSensor;
            inputSignalArray[4] = rightSensor;
        }

        protected override void UseBlackBoxOutpts(ISignalArray outputSignalArray)
        {
            /*  Uncomment all this and comment out the rest of this function for manual driving. 
             *  
            //grab the input axes
            var steer = Input.GetAxis("Horizontal");
            var gas = Input.GetAxis("Vertical");

            //if they're hittin' the gas...
            if (gas != 0)
            {
                //take the throttle level (with keyboard, generally +1 if up, -1 if down)
                //  and multiply by speed and the timestep to get the distance moved this frame
                var moveDist = gas * Speed * Time.deltaTime;

                //now the turn amount, similar drill, just turnSpeed instead of speed
                //   we multiply in gas as well, which properly reverses the steering when going 
                //   backwards, and scales the turn amount with the speed
                var turnAngle = steer * TurnSpeed * Time.deltaTime * gas;

                //now apply 'em, starting with the turn           
                transform.Rotate(0, turnAngle, 0);

                //and now move forward by moveVect
                transform.Translate(Vector3.forward * moveDist);
            }
            */

            var steer = (float)outputSignalArray[0] * 2 - 1;
            var gas = (float)outputSignalArray[1] * 2 - 1;

            var moveDist = gas * Speed * Time.deltaTime;
            var turnAngle = steer * TurnSpeed * Time.deltaTime * gas;

            transform.Rotate(new Vector3(0, turnAngle, 0));
            transform.Translate(Vector3.forward * moveDist);
        }

        protected override void HandleIsActiveChanged(bool newIsActive)
        {
            if (newIsActive == false)
            {
                // the unit has been deactivated, IsActive was switched to false

                // reset transform
                transform.position = _initialPosition;
                transform.rotation = _initialRotation;

                // reset members
                Lap = 1;
                CurrentPiece = 0;
                LastPiece = 0;
                WallHits = 0;
                _movingForward = true;
            }

            // hide/show children 
            // the children happen to be the car meshes => we hide this Unit when IsActive turns false and show it when it turns true
            foreach (Transform t in transform)
            {
                t.gameObject.SetActive(newIsActive);
            }
        }

        public override float GetFitness()
        {
            // calculate a fitness value based on how many laps were driven, how many roads crossed and how many walls touched.

            if (Lap == 1 && CurrentPiece == 0)
            {
                return 0;
            }

            int piece = CurrentPiece;
            if (CurrentPiece == 0)
            {
                piece = 17;
            }

            float fit = Lap * piece - WallHits * 0.2f;
            if (fit > 0)
            {
                return fit;
            }
            return 0;
        }

        public void NewLap()
        {
            if (LastPiece > 2 && _movingForward)
            {
                Lap++;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsActive)
                return;

            if (collision.collider.CompareTag("Road"))
            {
                RoadPiece rp = collision.collider.GetComponent<RoadPiece>();
                //  print(collision.collider.tag + " " + rp.PieceNumber);

                if ((rp.PieceNumber != LastPiece) && (rp.PieceNumber == CurrentPiece + 1 || (_movingForward && rp.PieceNumber == 0)))
                {
                    LastPiece = CurrentPiece;
                    CurrentPiece = rp.PieceNumber;
                    _movingForward = true;
                }
                else
                {
                    _movingForward = false;
                }
                if (rp.PieceNumber == 0)
                {
                    CurrentPiece = 0;
                }
            }
            else if (collision.collider.CompareTag("Wall"))
            {
                WallHits++;
            }
        }
    }
}