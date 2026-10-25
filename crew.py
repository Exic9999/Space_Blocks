"""The Executor's pilots: roster, personalities and their radio chatter."""

CREW = [
    dict(name="Mira Voss",     pers="bold",     color=(255, 120, 110), join=0),
    dict(name="Tobias Kline",  pers="builder",  color=(255, 205, 110), join=0),
    dict(name="Zeph",          pers="dreamer",  color=(140, 210, 255), join=0),
    dict(name="Okoye Adeyemi", pers="veteran",  color=(150, 230, 160), join=150),
    dict(name="Pip",           pers="reckless", color=(230, 150, 255), join=600),
]

# patience: seconds idle before going independent (scaled by Crew Quarters)
# caution: fraction of fuel at which they head home; greed: cut they skim when independent
# far: preference for distant islands; stubborn: chance to refuse orders when independent
PERS = {
    "bold":     dict(patience=110, caution=0.25, greed=0.40, far=0.2, stubborn=0.45,
                     build=dict(E=3, L=3, T=1, C=1, H=1, S=1),
                     title="Bold, impatient ace"),
    "builder":  dict(patience=200, caution=0.40, greed=0.25, far=0.0, stubborn=0.20,
                     build=dict(C=3, T=2, H=2, L=1, E=1, S=1),
                     title="Methodical engineer"),
    "dreamer":  dict(patience=140, caution=0.30, greed=0.20, far=1.0, stubborn=0.35,
                     build=dict(E=2, T=3, C=1, L=1, H=1, S=1),
                     title="Wandering scout"),
    "veteran":  dict(patience=320, caution=0.45, greed=0.10, far=0.0, stubborn=0.10,
                     build=dict(S=2, H=3, L=2, E=1, T=1, C=2),
                     title="Loyal old hand"),
    "reckless": dict(patience=70,  caution=0.12, greed=0.50, far=0.6, stubborn=0.60,
                     build=dict(L=3, E=3, H=1, T=1, C=1, S=0.3),
                     title="Hot-headed rookie"),
}

LINES = {
    "bold": {
        "launch": ["Mira: Wheels up. Try to keep up, Executor.", "Mira: Engines hot — that rock's mine."],
        "return": ["Mira: Coming home heavy. Clear the bay.", "Mira: Hold has Aetherite. Tank me up."],
        "build": ["Mira bolted on a new {block}. Faster, always faster.", "Mira: a {block} — now we're talking."],
        "independent": ["Mira: Respectfully, Commander, I'm not waiting around. Going my own way.",
                        "Mira: Silence is not an order. I'm taking initiative."],
        "refuse": ["Mira: I've got my own plans, Commander.", "Mira: Not this time. I'm busy."],
        "obey": ["Mira: Fine. Your call, Jason.", "Mira: Copy that, Commander."],
        "found": ["Mira: Fresh island on my scope!"],
    },
    "builder": {
        "launch": ["Tobias: Systems nominal. Heading out.", "Tobias: Checklist done. Launching."],
        "return": ["Tobias: Cargo bay is full. Inbound.", "Tobias: Returning on schedule."],
        "build": ["Tobias installed a {block}. Measured twice.", "Tobias: new {block} fitted. Good steel."],
        "independent": ["Tobias: I'll just... keep working, Commander. Someone has to.",
                        "Tobias: No orders in a while. Filing my own flight plan."],
        "refuse": ["Tobias: I have a schedule, Commander.", "Tobias: Give me a minute to finish this."],
        "obey": ["Tobias: Understood, adjusting plan.", "Tobias: Right away."],
        "found": ["Tobias: Logging a new island. Lovely deposits."],
    },
    "dreamer": {
        "launch": ["Zeph: The Veil is singing today. Off I go.", "Zeph: Out among the floating gardens."],
        "return": ["Zeph: Coming back... reluctantly.", "Zeph: Brought you some starlight. Aetherite, I mean."],
        "build": ["Zeph tuned in a new {block}. It hums nicely.", "Zeph: this {block} feels right."],
        "independent": ["Zeph: The waterfalls called me, Commander. I had to go.",
                        "Zeph: You were quiet, so I wandered."],
        "refuse": ["Zeph: Sorry... the islands are calling.", "Zeph: Can it wait? The light is perfect."],
        "obey": ["Zeph: Okay, okay. Course set.", "Zeph: As you wish."],
        "found": ["Zeph: Oh... a whole new island. Waterfalls and everything."],
    },
    "veteran": {
        "launch": ["Okoye: Eyes open, everyone. Launching.", "Okoye: Steady as she goes."],
        "return": ["Okoye: Returning to the Executor.", "Okoye: Bringing the haul home."],
        "build": ["Okoye reinforced her ship with a {block}.", "Okoye: Good {block}. Old habits."],
        "independent": ["Okoye: Commander, I trust you — but idle hands waste the Veil. Proceeding.",
                        "Okoye: I'll keep the lights on until you're ready."],
        "refuse": ["Okoye: Give me a moment, Commander.", "Okoye: Understood — after this run."],
        "obey": ["Okoye: Aye, Commander.", "Okoye: On it."],
        "found": ["Okoye: New island sighted. Stay sharp."],
    },
    "reckless": {
        "launch": ["Pip: Woooo! Launching!", "Pip: Watch me fly!"],
        "return": ["Pip: Coming in hot! Clear the bay!", "Pip: Full hold, nearly empty tank, classic."],
        "build": ["Pip slapped on a {block}. It probably fits.", "Pip: new {block}! Shiny!"],
        "independent": ["Pip: Nobody said no! Off I go!", "Pip: You were too slow, Commander. Bye!"],
        "refuse": ["Pip: Nope! Busy! Maybe later!", "Pip: I'm on a roll, Commander!"],
        "obey": ["Pip: Okay okay! Going!", "Pip: Yes sir, Commander sir!"],
        "found": ["Pip: Island! Island! Look at it!"],
    },
}
