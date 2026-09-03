using System;
using UnityEngine;

[Serializable]
public struct GameBallPresentationData
{
    [SerializeField] private int number;
    [SerializeField] private char letter;
    [SerializeField] private Color letterColor;

    public int Number => number;
    public char Letter => letter;
    public Color LetterColor => letterColor;
    public bool IsValid => number > 0 && letter != '\0';

    public GameBallPresentationData(int number, char letter, Color letterColor)
    {
        this.number = number;
        this.letter = letter;
        this.letterColor = letterColor;
    }
}
