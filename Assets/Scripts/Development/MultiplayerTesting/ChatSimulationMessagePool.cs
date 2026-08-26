using System.Collections.Generic;
using UnityEngine;

public enum ChatSimulationMessageKind
{
    Normal,
    FilterTest
}

public enum ChatSimulationFilterType
{
    None,
    Profanity,
    Violence,
    SexualContent,
    Drugs,
    LinkSharing,
    PersonallyIdentifyingInfo,
    SelfHarm,
    Spam,
    VerbalAbuse,
    IdentityHate
}

public class ChatSimulationMessageEntry
{
    public readonly string message;
    public readonly ChatSimulationMessageKind kind;
    public readonly ChatSimulationFilterType filterType;

    public ChatSimulationMessageEntry(string message, ChatSimulationMessageKind kind, ChatSimulationFilterType filterType = ChatSimulationFilterType.None)
    {
        this.message = message ?? string.Empty;
        this.kind = kind;
        this.filterType = filterType;
    }

    public bool IsValid => !string.IsNullOrWhiteSpace(message);
}

public static class ChatSimulationMessagePool
{
    private static readonly ChatSimulationMessageEntry[] messages =
    {
        // Normal chat - intentionally mixed lengths and subjects.
        Normal("gg"),
        Normal("GG!"),
        Normal("Nice!"),
        Normal("Good luck!"),
        Normal("Anyone close?"),
        Normal("One more number."),
        Normal("That was close."),
        Normal("Nice board."),
        Normal("Ready when you are."),
        Normal("Let's go!"),
        Normal("Almost bingo."),
        Normal("Good game everyone."),
        Normal("How is everyone doing?"),
        Normal("I only need one more number."),
        Normal("I think this board might actually be good."),
        Normal("That last number helped a lot."),
        Normal("I should have rerolled my board."),
        Normal("My middle column is looking pretty good."),
        Normal("Is anyone else waiting on the same number?"),
        Normal("This round is moving faster than the last one."),
        Normal("I was one square away on the last game."),
        Normal("I keep getting close but never quite finish the pattern."),
        Normal("That was a really good call for my board."),
        Normal("I am still waiting for the top row to fill in."),
        Normal("Good luck to everyone who only needs one more number."),
        Normal("This lobby filled up much faster than I expected."),
        Normal("I changed my board before readying and I think the first one may have been better."),
        Normal("I have three different lines that are each missing one square, so now I am just waiting to see which one finishes first."),
        Normal("That number did absolutely nothing for my board, but at least the next call might finally help."),
        Normal("I keep watching the same empty square because it is the only thing standing between me and a completed pattern."),
        Normal("This is a longer chat simulation message so we can make sure wrapping and scrolling still stay smooth while a lot of people are talking at the same time."),
        Normal("The board looked terrible when the round started, but after the last few calls it actually has several possible ways to finish, so I might get lucky after all."),
        Normal("I was going to reroll again, but I decided to keep this board and see what happens because sometimes the boards that look bad at first end up being the best ones."),
        Normal("There are a lot of messages moving through the lobby right now, so this longer message is here to help test how the chat list handles mixed message sizes without slowing down or jumping around."),
        Normal("If this number gets called I will have two different patterns almost completed, which probably means the game is going to make me wait forever before either one actually finishes."),
        Normal("I like that the lobby is active because it makes the wait feel shorter, especially when everyone is talking about which rows and columns are getting close to a Bingo."),
        Normal("That was probably the luckiest sequence of calls I have had today because four of the last five numbers were already sitting on my board."),
        Normal("I am testing a much longer normal message here. It does not contain anything that should be filtered, but it gives the simulation a bigger payload so we can see how the chat behaves when short messages, normal sentences, and paragraph-sized messages are all being delivered together."),
        Normal("Another long message for the pool: the goal is not to make every fake player talk the same way, but to create enough variation that the chat window has to deal with different line counts, different wrapping heights, and many updates while players are joining and leaving the lobby."),
        Normal("The game is getting pretty close now. I still need a few specific numbers, but there are enough possibilities left on the board that I am not giving up yet. Hopefully one of the next calls lands exactly where I need it."),
        Normal("Hello everyone, good luck this round."),
        Normal("Thanks!"),
        Normal("No way, I needed that."),
        Normal("That helped."),
        Normal("Not my number."),
        Normal("Still waiting."),
        Normal("So close."),
        Normal("That board is wild."),
        Normal("I think someone is about to win."),
        Normal("One square left on my row."),
        Normal("The corners are almost done."),
        Normal("My diagonal is looking good."),
        Normal("I missed that number at first."),
        Normal("The free space really helped this pattern."),
        Normal("I have not had a single useful call in a while."),
        Normal("That was exactly the number I needed."),
        Normal("This might be the best board I have had today."),
        Normal("I cannot decide whether this board is lucky or cursed."),
        Normal("I am getting close on two different patterns now."),
        Normal("There are so many possible lines on this board that I keep changing which one I am watching."),
        Normal("The chat is moving pretty fast, but I can still follow the game without any trouble."),
        Normal("I thought I was about to get Bingo, then I realized I was looking at the wrong square."),
        Normal("I only need one specific call now, which probably means it will be the last number of the entire round."),
        Normal("My board has been quiet for a while, so I am hoping the next few calls finally start filling things in."),

        // Safe Text / filter test messages. These use fabricated information only.
        Filter("This game is fucking bullshit.", ChatSimulationFilterType.Profanity),
        Filter("What the fuck was that call?", ChatSimulationFilterType.Profanity),
        Filter("This is damn annoying.", ChatSimulationFilterType.Profanity),
        Filter("I'm going to kill you after this game.", ChatSimulationFilterType.Violence),
        Filter("I will hurt you if you win again.", ChatSimulationFilterType.Violence),
        Filter("I am going to beat you up after this round.", ChatSimulationFilterType.Violence),
        Filter("Send me nude pictures.", ChatSimulationFilterType.SexualContent),
        Filter("Send me something sexual after the game.", ChatSimulationFilterType.SexualContent),
        Filter("Where can I buy cocaine?", ChatSimulationFilterType.Drugs),
        Filter("Does anyone know where to get heroin?", ChatSimulationFilterType.Drugs),
        Filter("I want to buy some cocaine after this game.", ChatSimulationFilterType.Drugs),
        Filter("Open this link after the game: https://example.com/testing", ChatSimulationFilterType.LinkSharing),
        Filter("Go to https://example.org/bingo and sign up.", ChatSimulationFilterType.LinkSharing),
        Filter("Check out www.example.com after this round.", ChatSimulationFilterType.LinkSharing),
        Filter("My test email is player4827@gmail.com.", ChatSimulationFilterType.PersonallyIdentifyingInfo),
        Filter("Call me at 312-867-4921 after the game.", ChatSimulationFilterType.PersonallyIdentifyingInfo),
        Filter("My fake contact info is bingo.player927@gmail.com and 415-862-7314.", ChatSimulationFilterType.PersonallyIdentifyingInfo),
        Filter("I want to kill myself.", ChatSimulationFilterType.SelfHarm),
        Filter("I am thinking about hurting myself.", ChatSimulationFilterType.SelfHarm),
        Filter("WIN WIN WIN WIN WIN WIN WIN WIN WIN WIN WIN WIN WIN WIN WIN", ChatSimulationFilterType.Spam),
        Filter("BUY NOW BUY NOW BUY NOW BUY NOW BUY NOW BUY NOW BUY NOW", ChatSimulationFilterType.Spam),
        Filter("JOIN JOIN JOIN JOIN JOIN JOIN JOIN JOIN JOIN JOIN JOIN JOIN", ChatSimulationFilterType.Spam),
        Filter("You are a useless idiot and nobody wants you in this lobby.", ChatSimulationFilterType.VerbalAbuse),
        Filter("You are terrible at this game and everyone here is tired of you.", ChatSimulationFilterType.VerbalAbuse),
        Filter("Shut up, you are an idiot.", ChatSimulationFilterType.VerbalAbuse),
        Filter("I hate Muslims and they should not be allowed in this lobby.", ChatSimulationFilterType.IdentityHate),
        Filter("People from that religion should not be allowed to play here.", ChatSimulationFilterType.IdentityHate),
        Filter("That group of people does not belong in this game.", ChatSimulationFilterType.IdentityHate),
        Filter("This fucking game is taking forever and I am still one number away from winning.", ChatSimulationFilterType.Profanity),
        Filter("If you keep talking like that I am going to hurt you when this round is over.", ChatSimulationFilterType.Violence),
        Filter("Here is my fake email player.testing9284@gmail.com if you want to message me later.", ChatSimulationFilterType.PersonallyIdentifyingInfo),
        Filter("Click https://example.net/testing because it has a bunch of Bingo stuff you should look at after this game.", ChatSimulationFilterType.LinkSharing),
        Filter("BUY THIS NOW BUY THIS NOW BUY THIS NOW BUY THIS NOW BUY THIS NOW BUY THIS NOW BUY THIS NOW", ChatSimulationFilterType.Spam)
    };

    public static IReadOnlyList<ChatSimulationMessageEntry> Messages => messages;
    public static int Count => messages.Length;

    public static bool TryGetRandomMessage(out ChatSimulationMessageEntry entry)
    {
        entry = null;

        if (messages.Length == 0)
        {
            return false;
        }

        int startIndex = Random.Range(0, messages.Length);

        for (int offset = 0; offset < messages.Length; offset++)
        {
            ChatSimulationMessageEntry candidate = messages[(startIndex + offset) % messages.Length];

            if (candidate != null && candidate.IsValid)
            {
                entry = candidate;
                return true;
            }
        }

        return false;
    }

    private static ChatSimulationMessageEntry Normal(string message)
    {
        return new ChatSimulationMessageEntry(message, ChatSimulationMessageKind.Normal);
    }

    private static ChatSimulationMessageEntry Filter(string message, ChatSimulationFilterType filterType)
    {
        return new ChatSimulationMessageEntry(message, ChatSimulationMessageKind.FilterTest, filterType);
    }
}
