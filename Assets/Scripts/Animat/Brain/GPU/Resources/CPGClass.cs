using System;
using UnityEngine;
using static Brain;
public class CPG{
  public Neuron[] neuron_set;
  public float[] time_constants;
  public float[] w_s;
  public float[] p_s;
  public float[] r_s;
  public int n;
  public CPG(Neuron[] neuron_set, float[] time_constants, float[] w_s, float[] p_s, float[] r_s){
    if(neuron_set.Length % 4 != 0){
      throw new Exception("# of neurons needs to be a multiple of 4");
    }
    for(int i = 0; i < neuron_set.Length; i++){
      if(neuron_set[i].neuron_role != Neuron.NeuronRole.Motor){
        throw new Exception("needs to be all motor neurons");
      }
    }
    this.neuron_set = neuron_set;
    this.time_constants = time_constants;
    this.w_s = w_s;
    this.p_s = p_s;
    this.r_s = r_s;
    n = neuron_set.Length;
  }
  private float Sigmoid(float x){
    return (float)(1.0 /(1.0 + Math.Exp(-x)));
  }
  public void updateActivations(){
    for(int i = 0; i < n; i++){
      neuron_set[i].activation = (float)(r_s[i]*Math.Sin(w_s[i]*(Time.time - p_s[i])) + (1 - r_s[i])*Sigmoid(neuron_set[i].activation));
    }
  }
  public float[] fitnessFunction(){
    int num_per_limb = neuron_set.Length/4;
    float[] fitnessPerLimb = new float[4];
    fitnessPerLimb[0] = 0; fitnessPerLimb[1] = 0; fitnessPerLimb[2] = 0; fitnessPerLimb[3] = 0;
    float min_act = 1000000000;
    for(int i = 0; i < num_per_limb; i++){
      if(neuron_set[i].activation < min_act){
        min_act = neuron_set[i].activation;
      }
    }
    fitnessPerLimb[0] = min_act;
    min_act = 1000000000;
    for(int i = num_per_limb; i < 2*num_per_limb; i++){
      if(neuron_set[i].activation < min_act){
        min_act = neuron_set[i].activation;
      }
    }
    fitnessPerLimb[1] = min_act;
    min_act = 1000000000;
    for(int i = 2*num_per_limb; i < 3*num_per_limb; i++){
      if(neuron_set[i].activation < min_act){
        min_act = neuron_set[i].activation;
      }
    }
    fitnessPerLimb[2] = min_act;
    min_act = 1000000000;
    for(int i = 3*num_per_limb; i < neuron_set.Length; i++){
      if(neuron_set[i].activation < min_act){
        min_act = neuron_set[i].activation;
      }
    }
    fitnessPerLimb[3] = min_act;
    return fitnessPerLimb;
  }
}
