using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GunFire : MonoBehaviour
{
    AudioSource GunSound;
    Animation GunAnimation;
    public GameObject ProjectilePreFab;

    // Start is called before the first frame update
    void Start()
    {
        GunAnimation = gameObject.GetComponent<Animation>();
        GunSound = gameObject.GetComponent<AudioSource>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetButtonDown("Fire1"))
        {
            GunSound.Play();
            GunAnimation.Play();

            //Transform projectileTransform = transform;
            //projectileTransform.Rotate(-90, 0, 0);

            //GameObject projectile = Instantiate(ProjectilePreFab, transform.position, transform.rotation);

            //projectile.GetComponent<Rigidbody>().AddForce(transform.forward * 2500);
        }
    }
}
