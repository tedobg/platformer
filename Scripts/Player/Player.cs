﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

[RequireComponent (typeof(Controller2D))]
[RequireComponent (typeof(MeleeAttackAgent))]
[RequireComponent(typeof(ExtendedAttackAgent))]
[RequireComponent(typeof(Hitbox))]
public class Player : MonoBehaviour
{
    [Header("General Movement Settings")]
    public LayerMask enemyCollisionMask;
    public Stat maxJumpHeight = 4;
    public Stat minJumpHeight = 1;
    public Stat timeToJumpApex = .4f;
    public Stat moveSpeed = 6;

    protected float lastAttack;

    [Header("Dash Settings")]
    public Stat dashSpeed = 30;
    public Stat dashDuration = .1f;
    public Stat dashCooldown = 2;
    float dashDurationLeft = 0;
    float dashCooldownLeft = 0;
    bool dashReset = true;

    [Header("Aerial Settings")]
    public Stat maxNumberOfAirJumps = 1;
    float jumpCount = 0;

    public Stat glideGravityModifier = .8f;
    public Stat glideMaxDuration = .8f;
    bool gliding = false;
    bool glidingReset = true;
    float glideRemainingDuration = 0;

    float accelerationTimeAirborne = .2f;
    float accelerationTimeGrounded = .1f;

    [Header("Wall Movement Settings")]
    public Vector2 wallJumpClimb;
    public Vector2 wallJumpOff;
    public Vector2 wallLeap;

    public Stat wallSlideSpeedMax = 3;
    public Stat wallStickTme = .25f;
    float timeToWallUnstick;

    float gravity;
    float maxJumpVelocity;
    float minJumpVelocity;
    Vector3 velocity;
    float velocityXSmoothing;

    [Header("Interactable Settings")]
    public float interactableDetectDistance = 2f;
    public LayerMask interactableMask;
    Interactable focus;

    [HideInInspector]
    public Controller2D controller;
    [HideInInspector]
    public Hitbox.Knockback currentKnockback;
    Hitbox hitbox;

    Vector2 directionalInput;
    bool wallSliding;
    int wallDirX;

    [HideInInspector]
    public MeleeAttackAgent lightAttack;
    [HideInInspector]
    public ExtendedAttackAgent heavyAttack;

    // Start is called before the first frame update
    void Start()
    {
        controller = GetComponent<Controller2D>();
        hitbox = GetComponent<Hitbox>();
        hitbox.onDamageTaken += OnDamageTaken;

        lightAttack = GetComponent<MeleeAttackAgent>();
        lightAttack.Init(enemyCollisionMask, controller.collisionMask);

        heavyAttack = GetComponent<ExtendedAttackAgent>();
        heavyAttack.Init(enemyCollisionMask, controller.collisionMask);

        RecalculateMovementSettings();
    }

    void Update()
    {
      
        CalculateVelocity();
        HandleWallSliding();

        controller.Move(velocity * Time.deltaTime, directionalInput, false);

        if (controller.collisions.above || controller.collisions.below)
        {
            if (controller.collisions.slidingDownMaxSlope)
            {
                velocity.y += controller.collisions.slopeNormal.y * -gravity * Time.deltaTime;
            }
            else
            {
                if(velocity.y < 0)
                    velocity.y = 0;
            }
        }

        if ((controller.collisions.below || controller.collisions.left || controller.collisions.right) && jumpCount > 0) 
        {
            jumpCount = 0;
            glidingReset = true;
        }
    }

    public void RecalculateMovementSettings()
    {
        gravity = -(2 * maxJumpHeight) / Mathf.Pow(timeToJumpApex, 2);
        maxJumpVelocity = Mathf.Abs(gravity) * timeToJumpApex;
        minJumpVelocity = Mathf.Sqrt(2 * Mathf.Abs(gravity) * minJumpHeight);
    }


    public void SetDirectionalInput (Vector2 input)
    {
        directionalInput = input;
    }

    public void OnDash()
    {
        if (dashDurationLeft <= 0 && dashCooldownLeft <= 0)
        {
            dashReset = false;
            dashDurationLeft = dashDuration;
            hitbox.Invulnerable(true);
        }
    }

    public void OnGlide()
    {
        if (!controller.collisions.below && !gliding && glidingReset && velocity.y <= 0)
        {
            glideRemainingDuration = glideMaxDuration;
            gliding = true;
        }

        if(gliding)
        {
            if(glideRemainingDuration > 0)
            {
                glideRemainingDuration -= Time.deltaTime;
            } else
            {
                glideRemainingDuration = 0;
                gliding = false;
                glidingReset = false;
            }
        }
    }

    public void OnJumpInputDown()
    {
        if (dashDurationLeft > 0)
            return;

        if (wallSliding)
        {
            if (wallDirX == directionalInput.x)
            {
                velocity.x = -wallDirX * wallJumpClimb.x;
                velocity.y = wallJumpClimb.y;
            }
            else if (directionalInput.x == 0)
            {
                velocity.x = -wallDirX * wallJumpOff.x;
                velocity.y = wallJumpClimb.y;
            }
            else
            {
                velocity.x = -wallDirX * wallLeap.x;
                velocity.y = wallLeap.y;
            }
        }

        if (controller.collisions.below)
        {
            if (controller.collisions.slidingDownMaxSlope)
            {
                if(directionalInput.x != -Mathf.Sign(controller.collisions.slopeNormal.x))
                {
                    //not jumping against max slope
                    velocity.y = maxJumpVelocity * controller.collisions.slopeNormal.y;
                    velocity.x = maxJumpVelocity * controller.collisions.slopeNormal.x;
                }
            }
            else
            {
                velocity.y = maxJumpVelocity;
            }
        }

        if(!wallSliding && !controller.collisions.below && !controller.collisions.left && !controller.collisions.right && jumpCount < maxNumberOfAirJumps)
        {
            velocity.y = maxJumpVelocity;
            jumpCount++;
        }
    }

    public void OnJumpInputUp()
    {
        if (velocity.y > minJumpVelocity)
        {
            velocity.y = minJumpVelocity;
        }

        if(gliding)
        {
            gliding = false;
        }
    }

    public bool OnInteract()
    {
        Collider2D hit = FindClosestInteractable();
        if (hit)
        {
            Interactable interactable = hit.GetComponent<Interactable>();
            if (interactable != null)
            {
                SetInteractableFocus(interactable);
                return true;
            }
        }

        RemoveInteractableFocus();
        return false;
    }

    Collider2D FindClosestInteractable()
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, interactableDetectDistance, interactableMask);

        float minSqrtDistance = float.MaxValue;
        Collider2D target = null;

        for (int i = 0; i < colliders.Length; i++)
        {
            float distance = (transform.position - colliders[i].transform.position).magnitude;

            if (distance < minSqrtDistance)
            {
                minSqrtDistance = distance;
                target = colliders[i];
            }
        }

        return target;
    }

    void SetInteractableFocus(Interactable newFocus)
    {
        if (newFocus != focus)
        {
            if (focus != null)
            {
                focus.onDefocused();
            }

            focus = newFocus;
        }

        focus.OnFocused(transform);
    }

    void RemoveInteractableFocus()
    {
        if (focus != null)
        {
            focus.onDefocused();
        }
        focus = null;
    }

    public void OnHeavyAttack()
    {
        if (dashDurationLeft > 0)
            return;

        float boundsSizeX = controller.ctrlCollider.bounds.size.x / 2;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, boundsSizeX + heavyAttack.range, enemyCollisionMask);

        if (hits.Length > 0)
        {
            Collider2D hit = hits[Random.Range(0, hits.Length)];
            heavyAttack.Attack(hit.transform);
        }
        else
        {
            heavyAttack.Attack();
        }
    }

    public void OnLightAttack(MeleeAttackAgent.AttackType attackType)
    {
        if (dashDurationLeft > 0)
            return;

        float boundsSizeX = controller.ctrlCollider.bounds.size.x / 2;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, boundsSizeX + lightAttack.range, enemyCollisionMask);

        if (hits.Length > 0)
        {
            Collider2D hit = hits[Random.Range(0, hits.Length)];
            lightAttack.Attack(hit.transform, attackType);
        } else
        {
            lightAttack.Attack(attackType);
        }
        
    }

    void HandleWallSliding()
    {
        wallDirX = (controller.collisions.left) ? -1 : 1;
        wallSliding = false;
        if ((controller.collisions.left || controller.collisions.right) && !controller.collisions.below && velocity.y < 0)
        {
            wallSliding = true;

            if (velocity.y < -wallSlideSpeedMax)
            {
                velocity.y = -wallSlideSpeedMax;
            }

            if (timeToWallUnstick > 0)
            {
                velocityXSmoothing = 0;
                velocity.x = 0;

                if (directionalInput.x != wallDirX && directionalInput.x != 0)
                {
                    timeToWallUnstick -= Time.deltaTime;
                }
                else
                {
                    timeToWallUnstick = wallStickTme;
                }
            }
            else
            {
                timeToWallUnstick = wallStickTme;
            }
        }
    }

    void CalculateVelocity()
    {
        float targetVelocityX = directionalInput.x * moveSpeed;

        if (currentKnockback.duration > 0)
        {
            if(currentKnockback.direction.x == 0)
            {
                currentKnockback.direction.x = - controller.collisions.faceDir;
            }

            float dirX = Mathf.Sign(currentKnockback.direction.x);
            float angle = -dirX * Hitbox.Knockback.Angle;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            Vector2 baseDirection = dirX * Vector2.right * currentKnockback.speed;

            Vector2 targetVelocity = new Vector2(baseDirection.x * cos + baseDirection.y * sin, -baseDirection.x * sin + baseDirection.y * cos);
            targetVelocityX = targetVelocity.x;
            velocity.y = currentKnockback.direction.y < 0 ? currentKnockback.direction.y : targetVelocity.y;

            currentKnockback.duration -= Time.deltaTime;
        } else
        {
            currentKnockback.duration = 0;
        }

        velocity.x = Mathf.SmoothDamp(velocity.x, targetVelocityX, ref velocityXSmoothing, (controller.collisions.below) ? accelerationTimeGrounded : accelerationTimeAirborne);

        bool dashing = false;
        if(dashDurationLeft > 0)
        {
            if(controller.collisions.left || controller.collisions.right)
            {
                dashDurationLeft = 0;
            } else
            {
                dashDurationLeft -= Time.deltaTime;
                velocity.x = dashSpeed * controller.collisions.faceDir;
                dashing = true;
            }  
        } 

        if(!dashing)
        {
            if(!dashReset)
            {
                dashCooldownLeft = dashCooldown;
                dashReset = true;
                hitbox.Invulnerable(false);
            } else
            {
                if(dashCooldownLeft >= 0)
                {
                    dashCooldownLeft -= Time.deltaTime;
                }
            }

            velocity.y += gravity * Time.deltaTime;

            if(gliding)
            {
                velocity.y *= glideGravityModifier;
            }
        }
        
    }


    void OnDamageTaken(float amount, Hitbox.Knockback knockback) {
        currentKnockback = knockback;
    }
}
