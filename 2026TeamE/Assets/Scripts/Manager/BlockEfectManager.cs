using UnityEngine;

public enum EffectType
{
    Dirt,
    Ore,
    Stone,
    HardRock
}

public class BlockEffectManager : MonoBehaviour
{
    public static BlockEffectManager Instance;

    [Header("種類ごとのパーティクル")]
    [SerializeField] private ParticleSystem dirtParticle;
    [SerializeField] private ParticleSystem oreParticle;
    [SerializeField] private ParticleSystem stoneParticle;
    [SerializeField] private ParticleSystem hardRockParticle;

    [Header("1ブロックあたりのエフェクト発生数")]
    [SerializeField] private int particlesPerBlock = 3; 

    private void Awake()
    {
        Instance = this;

        
        DisableAutoEmission(dirtParticle);
        DisableAutoEmission(oreParticle);
        DisableAutoEmission(stoneParticle);
        DisableAutoEmission(hardRockParticle);
    }

    private void DisableAutoEmission(ParticleSystem ps)
    {
        if (ps == null) return;
        var em = ps.emission;
        em.enabled = false;
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
    }

    public void PlayEffectAt(Vector3 worldPosition, EffectType type)
    {
        ParticleSystem targetParticle = null;

        if (type == EffectType.Dirt)
        {
            targetParticle = dirtParticle;
        }
        else if (type == EffectType.Ore)
        {
            targetParticle = oreParticle;
        }
        else if (type == EffectType.Stone)
        {
            targetParticle = stoneParticle;
        }
        else if (type == EffectType.HardRock)
        {
            targetParticle = hardRockParticle;
        }

        if (targetParticle == null) return;

        // X座標を固定せず、ブロックの実際の座標からパーティクルを出す
        targetParticle.transform.position = worldPosition;
        
        targetParticle.Emit(1); // パーティクルの数を減らす（元は particlesPerBlock (3) など）
    }
}
