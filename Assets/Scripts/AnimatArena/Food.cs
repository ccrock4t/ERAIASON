using UnityEngine;
using static WorldAutomaton.Elemental;

public class Food : MonoBehaviour
{
    public AnimatArena arena;
    public float nutrition_remaining = AnimatBody.ENERGY_IN_A_FOOD;
    const float MAX_ENERGY_FROM_GROWTH = AnimatBody.ENERGY_IN_A_FOOD;
    const float food_growth_rate = 0.01f;//  growth  per .02 sec

    public Vector3 original_position;

    public bool held = false;

    // Start is called before the first frame update
    void Start()
    {
        this.transform.gameObject.tag = "Food";
        ResetFood(AnimatBody.ENERGY_IN_A_FOOD);
    }

    public void ResetFood(float food_amount=1)
    {
        nutrition_remaining = food_amount;
        UpdateScaleAndPosition();
    }

    public void RemoveNutrition(float amount_to_remove)
    {
        nutrition_remaining -= amount_to_remove;
        UpdateScaleAndPosition();
    }

    public void UpdateScaleAndPosition()
    {
        Vector3 new_scale;

    
        new_scale = 0.5f*Vector3.one * nutrition_remaining / AnimatBody.ENERGY_IN_A_FOOD + 1.0f * Vector3.one;
        
        this.transform.localScale = new_scale;

        if (!held)
        {
            this.transform.position = new Vector3(this.transform.position.x, original_position.y + this.transform.localScale.y / 2f, this.transform.position.z);
        }
        else
        {
            //this.transform.position = new Vector3(this.transform.position.x, original_position.y + this.transform.localScale.y / 2f, this.transform.position.z);
        }
        
    }



    void FixedUpdate()
    {
        if (nutrition_remaining < MAX_ENERGY_FROM_GROWTH)
        {
            nutrition_remaining += food_growth_rate;
        }
        UpdateScaleAndPosition();


        Vector3Int voxel_position = new((int)original_position.x, (int)original_position.y, (int)original_position.z);
        if (!GlobalConfig.world_automaton.IsOutOfBounds(voxel_position))
        {
            Element element = GlobalConfig.world_automaton.GetCellCurrentState(voxel_position);
            if (element != Element.Empty)
            {
                original_position.y += 1;
            }

        }

        Vector3Int below_voxel_position = new((int)original_position.x, (int)original_position.y - 1, (int)original_position.z);
        if (!GlobalConfig.world_automaton.IsOutOfBounds(below_voxel_position))
        {
            Element element = GlobalConfig.world_automaton.GetCellCurrentState(below_voxel_position);
            if (element == Element.Empty)
            {
                original_position.y -= 1;
            }

        }
        

    }

}
