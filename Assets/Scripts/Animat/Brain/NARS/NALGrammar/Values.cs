/*
    Author: Christian Hahm
    Created: May 13, 2022
    Purpose: Enforces Narsese grammar that == used throughout the project
*/

/*
    <frequency, confidence>
*/
public struct EvidentialValue
{
    public float frequency;
    public float confidence;
    public EvidentialValue(float frequency=1.0f, float confidence=0.9f)
    {
        if (confidence >= 1.0f) confidence = 0.9999f;
        if(confidence <= 0.0f) confidence = 0.0001f;
        Asserts.assert(frequency >= 0.0 && frequency <= 1.0, "ERROR: Frequency " + frequency.ToString() + " must be in [0,1]");
        Asserts.assert(confidence >= 0.0 && confidence < 1.0, "ERROR: Confidence must be in (0,1)");
        this.frequency = frequency;
        this.confidence = confidence;
    }

    public string get_formatted_string() {
     
        string str = SyntaxUtils.stringValueOf(StatementSyntax.TruthValMarker)
            + this.frequency.ToString("F3")
            + SyntaxUtils.stringValueOf(StatementSyntax.ValueSeparator)
            + this.confidence.ToString("F3")
            + SyntaxUtils.stringValueOf(StatementSyntax.TruthValMarker);
        
        return str;
    }


    public override string ToString()
    {
        return this.get_formatted_string();
    }
        
}