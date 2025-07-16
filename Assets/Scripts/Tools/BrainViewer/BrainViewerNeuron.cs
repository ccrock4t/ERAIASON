using UnityEngine;

public class BrainViewerNeuron : MonoBehaviour
{
    public Brain.Neuron neuron;
    public SpriteRenderer SR;

    // Start is called before the first frame update
    void Start()
    {
        this.SR = this.GetComponent<SpriteRenderer>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void UpdateColor()
    {
        Color color;
   
        if(neuron.activation >= 0)
        {
            color = new Color(0, (float)neuron.activation, 0, 1);
        }
        else
        {
            color = new Color(-1* (float)neuron.activation, 0, 0, 1);
        }
            

        if(this.SR == null) this.SR = this.GetComponent<SpriteRenderer>();
        this.SR.color = color;
    }
}
