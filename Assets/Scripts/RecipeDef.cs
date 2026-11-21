using System.Text;
using UnityEngine;

[System.Serializable]
public struct Ingredient
{
    public ItemDef item;
    public int count;

    public Ingredient(ItemDef item, int count)
    {
        this.item = item;
        this.count = count;
    }
}

[CreateAssetMenu(menuName = "NodeLoad/Recipe")]
public class RecipeDef : ScriptableObject
{
    public Ingredient[] inputs;
    public ItemDef output;
    public int outputCount = 1;   // how many of the output one batch makes
    public float time = 2f;
    public string verb = "Working";

    public static RecipeDef Make(Ingredient[] inputs, ItemDef output, float time, string verb)
    {
        var d = CreateInstance<RecipeDef>();
        d.name = verb;
        d.inputs = inputs;
        d.output = output;
        d.time = time;
        d.verb = verb;
        return d;
    }

    public string Describe()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < inputs.Length; i++)
        {
            if (i > 0) sb.Append(" + ");
            if (inputs[i].count > 1) sb.Append(inputs[i].count).Append(' ');
            sb.Append(inputs[i].item.name);
        }
        sb.Append(" -> ");
        if (outputCount > 1) sb.Append(outputCount).Append(' ');
        return sb.Append(output.name).ToString();
    }
}
