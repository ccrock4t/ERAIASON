using UnityEngine;
public class CPG{
  public Neuron[] neuron_set;
  public float[] time_constants;
  public float[] w_s;
  public float[] p_s;
  public float[] r_s;
  public int n;
  public CPG(Neuron[] neuron_set, float[] time_constants, float[] w_s, float[] p_s, float[] r_s){
    for(int i = 0; i < neuron_set.length; i++){
      if(neuron_set[i].neuron_role != 2){
        throw new Exception("Ahhhhhhhhhhhhhhhhhhhh!!!!!!!!!!!!!!!!!!");//TODO: Come up with a better exception message
      }
    }
    this->neuron_set = neuron_set;
    this->time_constants = time_constants;
    this->w_s = w_s;
    this->p_s = p_s;
    this->r_s = r_s;
    n = neuron_set.length;
  }
  private float Sigmoid(float x){
    return 1.0/(1.0 + Math.Exp(-x));
  }
  public void updateActivations(){
    for(int i = 0; i < n; i++){
      neuron_set[i].activation = r_s[i]*Math.Sin(w_s[i]*(Time.Time - p_s[i])) + (1 - r_s[i])*Sigmoid(neuron_set[i].activation);
    }
  }
  public virtual float fitnessFunction() = 0;
  public float simulate(int num_iters){
    float sum = 0f;
    for(int iter = 0; iter < num_iters; iter++){
      updateActivations();
      sum += fitnessFunction();
    }
    return sum;
  }
}
