using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;

public class PokemonDataSerch : MonoBehaviour
{
    public int pokemonid = 25;
    public TextAsset jsonFile;
    public CharacterCombatStats enemydatas;
    public class Names
    {
        public string english;
        
    }

    public class Data
    {
        public int id;
        public Names name;

        public string[] type;

        [JsonProperty("base")]
        public Dictionary<string,int> stats;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void Read()
    {
        Data[] allPokemon = JsonConvert.DeserializeObject<Data[]>(jsonFile.text);
        foreach(Data pokemon in allPokemon)
        {
            if(pokemon.id == pokemonid)
            {
                enemydatas.displayName = pokemon.name.english;
                enemydatas.elementalTypes = pokemon.type;
                enemydatas.maximumHealth = pokemon.stats["HP"];
                enemydatas.attack = pokemon.stats["Attack"];
                enemydatas.defense = pokemon.stats["Defense"];

                return;
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
