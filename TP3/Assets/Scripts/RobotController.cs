using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public class RobotController : MonoBehaviour
{
    // D�claration des constantes
    private static readonly Vector3 FlipRotation = new Vector3(0, 180, 0);
    private static readonly Vector3 CameraPosition = new Vector3(0, 0, 0);
    private static readonly Vector3 InverseCameraPosition = new Vector3(0, 0, 0);

    // D�claration des variables
    public bool isSpeedBoosted = false;
    public bool isRolling = false;
    public bool isStandby = false;
    public bool _Grounded { get; set; }
    bool _Flipped { get; set; }
    bool _UpsideDown { get; set; }
    Animator _Anim { get; set; }
    Rigidbody _Rb { get; set; }
    Camera _MainCamera { get; set; }

    AudioSource _Audio; 

    // Valeurs expos�es
    [SerializeField]
    public float Speed;

    [SerializeField]
    public float baseSpeed;

    [SerializeField]
    float JumpForce = 0.1f;

    [SerializeField]
    LayerMask WhatIsGround;

    [SerializeField]
    public bool hasShield = false;

    [SerializeField]
    GameObject Shield;

    public static RobotController instance;

    private float baseGravity = -1.981f;

    public float smoothCameraTransitionSpeed = 0.125f;
    public Vector3 offset;
    private float lastCameraYPosition = 0;

    // Awake se produit avait le Start. Il peut �tre bien de r�gler les r�f�rences dans cette section.
    void Awake()
    {
        instance = this;
        _Anim = GetComponent<Animator>();
        _Anim.SetBool("Open_Anim", true);
        _Rb = gameObject.GetComponent<Rigidbody>();
        _MainCamera = Camera.main;
        _Audio = GetComponent<AudioSource>();
    }

    // Utile pour r�gler des valeurs aux objets
    void Start()
    {
        _Grounded = false;
        _Flipped = false;
        Speed = 50f;
        baseSpeed = 50f;
    }

    // V�rifie les entr�es de commandes du joueur
    void Update()
    {
        if (Shield != null)
        {
            Shield.SetActive(hasShield);
        }

        if (AnimatorIsPlaying("anim_open")) return;

        int direction = 0;

        CheckStandby();
        HorizontalMove(direction);
        CheckJump();
        CheckY();
        
    }

    private void LateUpdate()
    {
        UpdateCamera();
        CheckRolling();
    }

    void UpdateCamera()
    {
        float damping = 0.250f;
        Vector3 currentPosition = _MainCamera.transform.position;
        Vector3 targetPosition = _Anim.transform.position + offset;
        Vector3 smoothedPosition = Vector3.Lerp(_MainCamera.transform.position, targetPosition, damping);
        if (!_Grounded && (Math.Abs(lastCameraYPosition - targetPosition.y) < 30) && !AnimatorIsPlaying("anim_open"))
        {
            smoothedPosition.y = lastCameraYPosition;
        }
        else
        {
        _MainCamera.transform.LookAt(_Anim.transform);

        }
            _MainCamera.transform.position = new Vector3(targetPosition.x, smoothedPosition.y, smoothedPosition.z);
            lastCameraYPosition = _MainCamera.transform.position.y;
    }

    void CheckStandby() {
        if (Input.GetKeyDown(KeyCode.Tab) && Timer.instance != null) {
            isRolling = false;
            _Anim.SetBool("Roll_Anim", false);
            if(!isStandby) {
                isStandby = true; 
                Speed = 0f; 
                _Anim.SetBool("Open_Anim", false);
                Timer.instance.isStandby = true;
                BatteryProgressBar.instance.isStandby = true;
            } else {
                isStandby = false;
                Speed = baseSpeed;
                _Anim.SetBool("Open_Anim", true);
                Timer.instance.isStandby = false;
                BatteryProgressBar.instance.isStandby = false;
            }
        }
    }

    void CheckY() {
        if (gameObject.GetComponent<Transform>().position.y < -10) {
            Destroy(gameObject);
            LevelManager.instance.respawn();
        }
    }

    void ChangeSphereColliderToRoll() {
        SphereCollider sphereCollider = gameObject.GetComponent<SphereCollider>();
        sphereCollider.center = new Vector3 (0, .04f, 0);
        sphereCollider.radius = 0.04f;
    }

    void ChangeSphereColliderToWalk() {
        SphereCollider sphereCollider = gameObject.GetComponent<SphereCollider>();
        sphereCollider.center = new Vector3 (0, .058f, 0);
        sphereCollider.radius = 0.06f;
    }


    bool AnimatorIsPlaying(string stateName)
    {
        return _Anim.GetCurrentAnimatorStateInfo(0).IsName(stateName);
    }

    void HorizontalMove(int direction)
    {
        float absoluteZVelocity = Math.Abs(_Rb.velocity.z);

        if(!isStandby) {
            if (Input.GetKey(KeyCode.D))
		    {
                direction = 1; 
                if(_Grounded) _Anim.SetBool("Walk_Anim", true);
            }
            else if (Input.GetKey(KeyCode.A))
            {
                direction = -1;
                if (_Grounded) _Anim.SetBool("Walk_Anim", true);
            } else 
            {
                direction = 0;
                _Anim.SetBool("Walk_Anim", false);
            }

            if (direction == 0 && Input.GetKeyDown(KeyCode.LeftAlt)) {
                bool isRolling = _Anim.GetBool("Roll_Anim");
                if(isRolling) _Anim.SetBool("Roll_Anim", false);
                else _Anim.SetBool("Roll_Anim", true);
            }
           
        }
        

        AdaptSpeedToSlopes();
        FlipCharacter(direction);
        _Rb.AddForce(new Vector3(0, 0, direction * Speed), ForceMode.Acceleration);
    }

    void CheckRolling()
    {
        bool goToRollPlaying = AnimatorIsPlaying("anim_open_GoToRoll");
        if (_Anim.GetBool("Roll_Anim") && !goToRollPlaying)
        {
            isRolling = true;
            ChangeSphereColliderToRoll();
            Speed = baseSpeed * 2;
        } else
        {
            isRolling = false;
            ChangeSphereColliderToWalk();
            Speed = baseSpeed;
        }
    }

    void AdaptSpeedToSlopes() {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.TransformDirection(Vector3.forward), out hit, Mathf.Infinity, LayerMask.GetMask("Floor")))
        {
            Vector3 reflectVec = Vector3.Reflect(transform.TransformDirection(Vector3.forward), hit.normal);
            float angle = Vector3.Angle(reflectVec, transform.TransformDirection(Vector3.forward));
            if (180f-angle < 90f && hit.distance<0.5f) {
                Speed = 0;
            }
        } 
    }

    // G�re le saut du personnage, ainsi que son animation de saut
    void CheckJump()
    {
        if (_Grounded && !isStandby)
        {
            if (Input.GetButtonDown("Jump"))
            {
                if(baseGravity < 1) 
                {
                    _Rb.AddForce(new Vector3(0, JumpForce, 0), ForceMode.Impulse);
                    _Grounded = false;
                    _Anim.SetBool("Walk_Anim", false);
                }
                else
                {
                    _Rb.AddForce(new Vector3(0, -JumpForce, 0), ForceMode.Impulse);
                    _Grounded = false;
                    _Anim.SetBool("Walk_Anim", false);
                }
                _Audio.Play(0);
            }
        }
    }

    public void InvertGravity()
    {
        baseGravity *= -1;
    }

    
    // G�re l'orientation du joueur et les ajustements de la camera
    void FlipCharacter(int direction)
    {
        float initialYRotation = _MainCamera.transform.rotation.y;
        if (direction < 0 && !_Flipped)
        {
            _Flipped = true;
            transform.Rotate(FlipRotation);
            _MainCamera.transform.Rotate(-FlipRotation);
            _MainCamera.transform.localPosition = InverseCameraPosition;
        }
        else if (direction > 0 && _Flipped)
        {
            _Flipped = false;
            transform.Rotate(-FlipRotation);
            _MainCamera.transform.Rotate(FlipRotation);
            _MainCamera.transform.localPosition = CameraPosition;
        }
    }
   
// Collision avec le sol
void OnCollisionEnter(Collision coll)
    {
        if(coll.gameObject.tag == "Spaceship")
        {
            Debug.Log("Game won");
        }
        HealthBar.instance.ApplyDamage(coll);
        // On s'assure de bien �tre en contact avec le sol
        if ((WhatIsGround & (1 << coll.gameObject.layer)) == 0)
            return;

        // �vite une collision avec le plafond
        //if (coll.relativeVelocity.y > 0)
        //{
        _Grounded = true;
        //}
    }

    //



}