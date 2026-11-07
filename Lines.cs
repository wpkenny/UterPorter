namespace UterPorter
{
    /// <summary>What Uter says. "{name}" becomes his name (it's a setting), so the ride-app jokes still work if he's
    /// renamed. Written so they fit any player.</summary>
    public static class Lines
    {
        public static readonly string[] Hello =
        {
            "You rang? Where to?",
            "Taxi for one Viking, is it?",
            "Again? Right. Hop on.",
            "{name}'s here. Try not to wriggle.",
            "Back already? Climb aboard.",
            "Did someone call an {name}?",
            "Your {name} has arrived. Look for the grumpy Dverger.",
            "Your driver, {name}, is now arriving. Grey cloak, bad mood.",
            "{name} here. Five stars, or I drop you in a bog.",
            "Surge pricing's on, mind. It's raining greylings.",
        };

        public static readonly string[] PickUp =
        {
            "Up you get. Mind the beard.",
            "Fine. But I'm telling the other Dvergr about this.",
            "Climb on. Gently. I'm a porter, not a cart.",
            "Odin's eye, what have you been eating?",
            "Right. Hold on, and keep your elbows to yourself.",
            "On you hop, your majesty.",
            "If anyone asks, you're a very large sack of turnips.",
        };

        public static readonly string[] Riding =
        {
            "You know you have legs, yes? Two of them. Working ones.",
            "Lighter on the left, please. My left. Your other left.",
            "Stop kicking. I'm not a horse.",
            "Is that your knee in my kidney, or have you brought a hammer?",
            "Heavier than a troll and twice as chatty.",
            "Mighty Viking warrior, this one. Can't even walk home.",
            "Next time get a lox. The lox can carry you.",
            "If you dribble on my helmet, you're walking.",
            "Great heroes of the tenth world, chapter one: the piggyback.",
            "Don't pull my beard to steer. I know the way.",
            "Comfy up there? Wonderful. Delighted for you.",
            "Valhalla's halls will sing of this. Mockingly.",
            "Grip a bit looser. I'd like to breathe.",
            "You smell of greyling. Just so you know.",
            "Rate your {name} experience later. Hold on now.",
            "No, I don't take coin. I take complaints, and then I ignore them.",
            "Estimated arrival: whenever my knees say so.",
            "This is {name} Pool, by the way. Anyone we pass is getting on too.",
            "Please keep your arms inside the Dverger at all times.",
            "Your route has been updated. I've decided to go round the bog.",
            "Music? No. You get my breathing, and you'll like it.",
            "If you wanted luxury you should've booked {name} Black.",
        };

        public static readonly string[] Jump =
        {
            "Hup! Hold on!",
            "Oof. You felt that? So did I.",
            "Over we go. My knees send their regards.",
            "Log. Jumped it. You're welcome.",
        };

        public static readonly string[] Home =
        {
            "Home. Mind your step getting down.",
            "Here we are. Off. Off! My back is filing a complaint.",
            "Delivered. No tip expected. Some gratitude would be nice.",
            "There. Home. Next time, you carry me.",
            "Home at last. I can't feel my shoulders.",
            "You have arrived at your destination. Mind the step.",
            "That's the end of your {name}. Don't forget to rate me.",
            "Destination reached. I'll take my tip in mead.",
        };

        public static readonly string[] Grave =
        {
            "Here's where you died. Try not to make a habit of it.",
            "Your things are in there somewhere. Good luck.",
            "Smells like defeat round here. Grab your gear.",
            "There's your stone. I'd not hang about if I were you.",
            "Here lies you, apparently. Off you get.",
            "Destination: your own tombstone. Bit grim for a five-star trip.",
            "This isn't {name} Eats, but something round here clearly ate you.",
        };

        public static readonly string[] OffEarly =
        {
            "Oh, NOW you want to walk.",
            "Off already? My back thanks you.",
            "Right, legs work again, do they? Marvellous.",
            "Suit yourself. I wasn't enjoying it anyway.",
            "Cancelling the ride? There's a fee for that.",
            "Getting out early? I'm still charging the full trip.",
        };

        public static readonly string[] Farewell =
        {
            "Right, I'm off.",
            "Call me when you need hauling again.",
            "My back's going home now. Alone.",
            "Don't wave. I know you'll miss me.",
            "I'll call you an {name}. Oh wait, that's me. Bye.",
            "{name} out. Leave me a review.",
            "Off to my next pickup.",
            "Next time you need a ride, you know who to call.",
        };
    }
}
