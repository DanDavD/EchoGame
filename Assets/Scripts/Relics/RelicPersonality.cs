using System.Collections.Generic;
using UnityEngine;

namespace EchoProtocol.Relics
{
    /// <summary>
    /// Personality archetypes inspired by High on Life meta comedic style.
    /// </summary>
    public enum RelicPersonalityType
    {
        /// <summary>Obsidian Bob: Cynical, meta gamer roasts, Steam refunds, posture roasts.</summary>
        SarcasticCritic = 0,

        /// <summary>Nervous Nelly: Neurotic coward, horror tropes, Roblox comparisons, Steam skins.</summary>
        AnxiousCoward = 1,

        /// <summary>Gloop the Box: Complete idiot that babbles, makes cartoon noises, and celebrates physics.</summary>
        DumbGloop = 2,

        /// <summary>Lord Malakor: Pompous boomer emperor, threatens to curse Wi-Fi and analog stick drift.</summary>
        GrumpyElder = 3
    }

    /// <summary>
    /// Dialogue triggers based on physical and gameplay events.
    /// </summary>
    public enum RelicDialogueCategory
    {
        IdleSleep = 0,
        Pickup = 1,
        CarryChatter = 2,
        Sprinting = 3,
        MicReaction = 4,
        MonsterNear = 5,
        NormalDrop = 6,
        FlyingScream = 7,
        LandedImpact = 8,
        AltarPlaced = 9,
        /// <summary>Bursts into tears: the carrier was too loud, it landed hard, or nobody passed it around.</summary>
        Crying = 10,
        /// <summary>The team echoed its rhythm and it calms down.</summary>
        Soothed = 11,
        /// <summary>Same hands for too long: a warning to pass it on.</summary>
        Fussy = 12,
        /// <summary>Handed straight from one player to another.</summary>
        PassedAround = 13
    }

    /// <summary>
    /// Metadata and comedy line repository for living relics.
    /// </summary>
    public static class RelicPersonality
    {
        public struct Profile
        {
            public string Name;
            public string Title;
            public Color PrimaryColor;
            public Color BackgroundColor;
            public float BasePitch;
            public float PitchVariance;
        }

        private static readonly Dictionary<RelicPersonalityType, Profile> Profiles = new Dictionary<RelicPersonalityType, Profile>
        {
            [RelicPersonalityType.SarcasticCritic] = new Profile
            {
                Name = "OBSIDIAN BOB",
                Title = "Cynical Relic",
                PrimaryColor = new Color(1f, 0.72f, 0.19f, 1f), // Neon Amber
                BackgroundColor = new Color(0.12f, 0.09f, 0.04f, 0.92f),
                BasePitch = 0.95f,
                PitchVariance = 0.12f
            },
            [RelicPersonalityType.AnxiousCoward] = new Profile
            {
                Name = "NERVOUS NELLY",
                Title = "Panic Relic",
                PrimaryColor = new Color(0.18f, 0.88f, 1f, 1f), // Electric Cyan
                BackgroundColor = new Color(0.02f, 0.10f, 0.14f, 0.92f),
                BasePitch = 1.45f,
                PitchVariance = 0.28f
            },
            [RelicPersonalityType.DumbGloop] = new Profile
            {
                Name = "GLOOP",
                Title = "Goofy Box",
                PrimaryColor = new Color(0.35f, 1f, 0.25f, 1f), // Slime Green
                BackgroundColor = new Color(0.04f, 0.12f, 0.03f, 0.92f),
                BasePitch = 1.15f,
                PitchVariance = 0.45f
            },
            [RelicPersonalityType.GrumpyElder] = new Profile
            {
                Name = "LORD MALAKOR",
                Title = "Ancient Sovereign",
                PrimaryColor = new Color(0.78f, 0.35f, 1f, 1f), // Imperial Purple
                BackgroundColor = new Color(0.10f, 0.03f, 0.14f, 0.92f),
                BasePitch = 0.70f,
                PitchVariance = 0.08f
            }
        };

        private static readonly Dictionary<(RelicPersonalityType, RelicDialogueCategory), string[]> Lines =
            new Dictionary<(RelicPersonalityType, RelicDialogueCategory), string[]>
            {
                // =========================================================================
                // 1. SARCASTIC CRITIC (OBSIDIAN BOB)
                // =========================================================================
                [(RelicPersonalityType.SarcasticCritic, RelicDialogueCategory.IdleSleep)] = new[]
                {
                    "Still on the cold floor. Take your time, we only have 47 minutes before your Steam refund window closes.",
                    "Just lying here listening to you miss every single audio cue. It's truly breathtaking.",
                    "I've seen deeper lore and better tactics in a free Roblox obby than in your navigation.",
                    "If you leave me here much longer, I'm filing a grievance with the Unity developers."
                },
                [(RelicPersonalityType.SarcasticCritic, RelicDialogueCategory.Pickup)] = new[]
                {
                    "Oh wonderful, picked up by the guy who definitely leaves Motion Blur turned on.",
                    "Watch the grip! I'm ancient alien tech, not your crusty desktop stress ball.",
                    "Your palms are soaking wet. Are you nervous or did you just eat hot wings over the keyboard?",
                    "You bought a $200 headset just to walk directly into the only monster in the room. Incredible."
                },
                [(RelicPersonalityType.SarcasticCritic, RelicDialogueCategory.CarryChatter)] = new[]
                {
                    "Un-shrimp your posture. Your spine looks like a parenthesis and it's offending my runes.",
                    "Are you navigating with a map, or did you just close your eyes and pray to Gabe Newell?",
                    "Stop whimpering! Seriously, Roblox horror games are scarier than this and you don't hear 9-year-olds crying.",
                    "If we die here, I'm posting your gameplay clip on Reddit under 'What NOT to do'."
                },
                [(RelicPersonalityType.SarcasticCritic, RelicDialogueCategory.Sprinting)] = new[]
                {
                    "Whoa, look at that cardio! Two seconds of sprinting and you're already wheezing into the microphone!",
                    "Running won't fix your 0.4 K/D ratio, buddy, but keep burning those virtual calories!",
                    "Sprint faster! Maybe the wind resistance will blow away your terrible life choices!"
                },
                [(RelicPersonalityType.SarcasticCritic, RelicDialogueCategory.MicReaction)] = new[]
                {
                    "Mute your mic! You sound like an industrial Dyson vacuum cleaner snorting Dorito dust!",
                    "Could you breathe any louder?! The monster is blind, not deaf, and your nostrils are in 4K resolution!",
                    "Stop chewing on your microphone! Nobody in this lobby wants to hear your salivary glands at work!"
                },
                [(RelicPersonalityType.SarcasticCritic, RelicDialogueCategory.MonsterNear)] = new[]
                {
                    "He's right there. Don't move. Don't even twitch your greasy thumb on the WASD keys.",
                    "If he catches you, remember: dying to a blind enemy in 2026 is an automatic uninstall.",
                    "Quick, blame it on ping! Type 'lag spike' in chat before he digests us!"
                },
                [(RelicPersonalityType.SarcasticCritic, RelicDialogueCategory.NormalDrop)] = new[]
                {
                    "Right on the concrete. Fantastic. 5 stars on UberEats, zero tip.",
                    "Dropping me? What, did your mom call you down for dinner?",
                    "Leaving me here like an unranked teammate. Typical gamer commitment issues."
                },
                [(RelicPersonalityType.SarcasticCritic, RelicDialogueCategory.FlyingScream)] = new[]
                {
                    "WAAAAAAAGHHH! SWEAR ON YOUR STEAM ACCOUNT YOU DIDN'T DO THIS ON PURPOOOOSE! AAAAAHH!",
                    "YOU PAID TWENTY DOLLARS JUST TO YEET ME INTO THE CEILIIIIING?! AAAAAAAAGH!",
                    "I'M CLIPPING INTO THE BACKROOMS! UNITY RIGIDBODY TAKE THE WHEEEEEEL! AAAAAHHH!",
                    "NOT THE WALL! NOT THE-- MY HITBOX ISN'T RATED FOR THIIIIIS! AAAAAAAAAAGH!"
                },
                [(RelicPersonalityType.SarcasticCritic, RelicDialogueCategory.LandedImpact)] = new[]
                {
                    "Ow! My collision mesh! ...Pretty sure my specular map just shattered on impact.",
                    "Solid 10 out of 10 for distance. Minus 50 DKP for human decency.",
                    "If I get stuck in the floor geometry, I'm haunting your graphics card forever."
                },
                [(RelicPersonalityType.SarcasticCritic, RelicDialogueCategory.AltarPlaced)] = new[]
                {
                    "Oh thank god, a surface that isn't your clammy gamer palms. Now step away slowly and don't touch me.",
                    "Finally, an actual pedestal. Five stars for the altar geometry, minus 50 stars for the delivery boy.",
                    "I'm locked in! Wait, are you leaving me behind while you run for the exit?! That's so toxic, I respect it."
                },

                // =========================================================================
                // 2. ANXIOUS COWARD (NERVOUS NELLY)
                // =========================================================================
                [(RelicPersonalityType.AnxiousCoward, RelicDialogueCategory.IdleSleep)] = new[]
                {
                    "Is it gone?! Don't leave me alone, what if a low-poly bug crawls into my intake vents?!",
                    "I'm having a panic attack and I don't even have a nervous system! SOMEBODY HELP!",
                    "The shadows are moving! Or maybe the shadow resolution is just set to Low! Either way I'm terrified!"
                },
                [(RelicPersonalityType.AnxiousCoward, RelicDialogueCategory.Pickup)] = new[]
                {
                    "Please don't drop me! Swear on your Steam account you won't drop me! SWEAR IT!",
                    "Oh gosh oh gosh, you're holding me! Did you wash your hands?! What if the monster smells pizza rolls?!",
                    "Hold me tighter! Wait no, looser! Don't smudge my sacred finish with your thumb grease!"
                },
                [(RelicPersonalityType.AnxiousCoward, RelicDialogueCategory.CarryChatter)] = new[]
                {
                    "Did you hear that squelch?! That was definitely a murder-squelch! Why is this game so scary?!",
                    "Why didn't we play Minecraft instead?! In Minecraft you can just dig a hole and cry in the dirt!",
                    "Even Roblox Piggy has safe zones! WHERE IS OUR SAFE ZONE?! WHY IS THERE NO CHECKPOINT?!",
                    "If you die, promise me you'll refund the game so we both get deleted peacefully!"
                },
                [(RelicPersonalityType.AnxiousCoward, RelicDialogueCategory.Sprinting)] = new[]
                {
                    "WHY ARE WE RUNNING?! WHAT DID YOU HEAR?! IS IT SPRINTING TOO?! OH GOD IT HAS LEGS!",
                    "Too fast! Too fast! You're creating sonic turbulence! IT CAN SMELL OUR FRAME RATE DROPPING!",
                    "Slow down! My virtual organs are sloshing around like a lukewarm smoothie!"
                },
                [(RelicPersonalityType.AnxiousCoward, RelicDialogueCategory.MicReaction)] = new[]
                {
                    "SHHHHHHH! Hold your breath! Stop existing! HE CAN HEAR YOUR DNA VIBRATING IN 7.1 SURROUND SOUND!",
                    "Muzzle yourself! Your whisper sounds like a commercial Boeing 747 taking off inside a microwave!",
                    "Stop making wet mouth noises! Can you just swallow your spit in complete silence for FIVE SECONDS?!"
                },
                [(RelicPersonalityType.AnxiousCoward, RelicDialogueCategory.MonsterNear)] = new[]
                {
                    "IT'S RIGHT BEHIND US! DON'T LOOK! IF YOU DON'T LOOK IT DOESN'T EXIST! OBJECT PERMANENCE IS A LIE!",
                    "Tell my family I loved them! Wait, I'm a procedural prop, I DON'T HAVE A FAMILY! AAAAAAH!",
                    "Please don't eat me, Mr. Monster! I'm 90% high-density polyhedrons and zero nutritional value!"
                },
                [(RelicPersonalityType.AnxiousCoward, RelicDialogueCategory.NormalDrop)] = new[]
                {
                    "NOOO! Don't leave me on the dirty floor! What if a spider uses me as a penthouse?!",
                    "Pick me back up! The floor is cold and evil and completely devoid of love!",
                    "I'm vulnerable! I'm an orphaned loot drop in the dark! DON'T ABANDON ME!"
                },
                [(RelicPersonalityType.AnxiousCoward, RelicDialogueCategory.FlyingScream)] = new[]
                {
                    "WHYYYYYYYYYYYY?! SWEAR ON YOUR MOTHERBOARD WE AREN'T GOING TO DIEEEE! AAAAAAAAH!",
                    "I DON'T HAVE A PARACHUUUUTE! GOODBYE CRUEL UNITY SCENEEEEE! AAAAAAHHH!",
                    "TERMINAL VELOCITY WAS NOT IN MY TERMS OF SERVIIIIICE! MOMMYYYYYYY!"
                },
                [(RelicPersonalityType.AnxiousCoward, RelicDialogueCategory.LandedImpact)] = new[]
                {
                    "I'M ALIVE?! Wait, is that a dent?! AAAAAHH I'M LEAKING PRIMITIVE VERTICES!",
                    "My collision normal! It's completely inverted! Everything smells like burnt solder!",
                    "Never launch me like that again! Swear on your entire Steam library and your inventory skins!"
                },
                [(RelicPersonalityType.AnxiousCoward, RelicDialogueCategory.AltarPlaced)] = new[]
                {
                    "Is this an electric chair?! Why is it humming?! WHAT IF IT'S A SACRIFICIAL PEDESTAL?! UN-SOCKET ME!",
                    "Wait, you're plugging me in?! What if the monster likes altar snacks?! SWEAR ON YOUR MOTHERBOARD I WON'T GET EATEN!",
                    "It's vibrating! My polyhedrons are buzzing! Don't leave me alone in the middle of the room!"
                },

                // =========================================================================
                // 3. DUMB GLOOP (GLOOP THE BOX)
                // =========================================================================
                [(RelicPersonalityType.DumbGloop, RelicDialogueCategory.IdleSleep)] = new[]
                {
                    "*Snore* Honk shoo, honk shoo... *snort* ...Mmm, yummy floor dust, tastes like crunchy pixels.",
                    "Bloop! Nap time! If I don't move, the monster thinks I'm a fancy living room ottoman.",
                    "Zzzzz... *bubble pop* ...Are we at McDonald's yet? Get me a happy meal toy, please..."
                },
                [(RelicPersonalityType.DumbGloop, RelicDialogueCategory.Pickup)] = new[]
                {
                    "YAY! Picked up! Look at me, I'm luggage! Wheeeee! Pet the box!",
                    "Boing! Warm gamer paws! Are we best friends now? Say yes or I'll make kazoo noises!",
                    "Ooo, your hands are squishy! Squish squish! You have ten funny little sausage fingers!"
                },
                [(RelicPersonalityType.DumbGloop, RelicDialogueCategory.CarryChatter)] = new[]
                {
                    "Look at that wall! Hello wall! ...Wall didn't say hello back. That wall is toxic, report it!",
                    "Bloop bloop! I'm an aerodynamic square! *Makes helicopter noises* Fpth-fpth-fpth-fpth!",
                    "Is the monster our UberEats driver? Did you order pizza? I hope he brought extra garlic bread!",
                    "Why is everyone so scared? The dark is just spicy sunshine! *Burp* ...Excuse me."
                },
                [(RelicPersonalityType.DumbGloop, RelicDialogueCategory.Sprinting)] = new[]
                {
                    "NYOOOOM! Vroom vroom! Look at the floor go whoooosh! Faster horsey, FASTER!",
                    "Bababababa! We zoomin! Out of the way, physics engine! Beep beep!",
                    "Speedy square on the loose! I feel the need... the need for CHEDDAR CHEESE!"
                },
                [(RelicPersonalityType.DumbGloop, RelicDialogueCategory.MicReaction)] = new[]
                {
                    "Ooo noisy mouth friend! *blows loud raspberry into mic* PBBBBBBBTTTT! Did you like my song?!",
                    "Lalalala! Are we doing choir practice?! *sings off-key* I BELIEVE I CAN FLYYYYYYYY!",
                    "Honk honk! Squeaaak! You talk funny! Do the duck voice again! Quack quack quack!"
                },
                [(RelicPersonalityType.DumbGloop, RelicDialogueCategory.MonsterNear)] = new[]
                {
                    "Ooo big fuzzy boy! Give him a hug! He looks like he needs a gentle forehead kiss!",
                    "Hello tall puppy! Does he bite? Only one way to find out! Stick your hand right in his mouth!",
                    "Look at his teef! So pointy! He needs a dentist appointment and some flossing!"
                },
                [(RelicPersonalityType.DumbGloop, RelicDialogueCategory.NormalDrop)] = new[]
                {
                    "Plop! Floor taste like floor. 10 out of 10, would lick again!",
                    "Splat! Wheee, I'm a pancake now! Flip me over when I'm golden brown!",
                    "Nap time on the dirt! Goodnight moon, goodnight scary monster with 40 sharp teeth!"
                },
                [(RelicPersonalityType.DumbGloop, RelicDialogueCategory.FlyingScream)] = new[]
                {
                    "WHEEEEEEEEEEEEEE! LOOK MA, NO GRAVITYYYY! WAHOOOOOOO! NYOOOOM!",
                    "I'M A METEORRRRR! KABOOOOOOM INCOMINGGGGG! CLEAR THE RUNWAYYYY!",
                    "I CAN SEE MY HOUSE FROM HEREEEEE! *KAZOO SOLOOOO* BZZZZT BOING BOING!"
                },
                [(RelicPersonalityType.DumbGloop, RelicDialogueCategory.LandedImpact)] = new[]
                {
                    "BOING! Bonk! Hehehe... My head went all squishy! Throw me again, DO IT AGAIN!",
                    "Ta-daaa! 10 out of 10 gymnastics! Now throw me straight into the ceiling light!",
                    "Oof! That made a funny squeak! *squeaks pathetically* Squeeeek!"
                },
                [(RelicPersonalityType.DumbGloop, RelicDialogueCategory.AltarPlaced)] = new[]
                {
                    "Oooo! Fancy booster seat! Look at me, I'm an ancient museum piece now! Wheeeee!",
                    "Boing! Fits like a glove! Can I push the glowing buttons? Beep boop beep boop!",
                    "We did teamwork! Teamwork makes the dream work! Now go pet the fuzzy teeth monster as celebration!"
                },

                // =========================================================================
                // 4. GRUMPY ANCIENT BOOMER (LORD MALAKOR)
                // =========================================================================
                [(RelicPersonalityType.GrumpyElder, RelicDialogueCategory.IdleSleep)] = new[]
                {
                    "Abandoned upon the detritus. The youth of this generation have zero respect for celestial dynasties.",
                    "A thousand millennia slumbering, only to be left on the floor like an uncharged vape pen.",
                    "If I am not lifted within ten seconds, I shall invoke the ancient curse of stick drift upon your controller."
                },
                [(RelicPersonalityType.GrumpyElder, RelicDialogueCategory.Pickup)] = new[]
                {
                    "Unhand me, peasant! I conquered seven cosmic realms and you handle me like a soggy Amazon delivery!",
                    "Where are your velvet gloves, you insolent primate?! Your Cheeto dust defiles my royal casing!",
                    "Grip me by the gilded bevels, serf, not by the sacred rune seal! Uncouth barbarian!"
                },
                [(RelicPersonalityType.GrumpyElder, RelicDialogueCategory.CarryChatter)] = new[]
                {
                    "Back in my epoch, vessels glided with majesty. Now look at you, scuttling like a startled cockroach.",
                    "Your gait is terribly un-regal. Fix your spine, serf, a sovereign does not travel upon a bent shrimp!",
                    "I have consulted the celestial oracles: they confirm your gameplay is thoroughly mediocre.",
                    "You tremble before a single blind beast? In my day, we conquered leviathans before breakfast!"
                },
                [(RelicPersonalityType.GrumpyElder, RelicDialogueCategory.Sprinting)] = new[]
                {
                    "HALT! Cease this undignified scampering! A monarch does not jog like a discount marathon runner!",
                    "You are rattling my cosmic gyroscope! Slow your frantic pace before I smite your motherboard!",
                    "Run with decorum, peasant! You sound like a sack of wet potatoes rolling down a staircase!"
                },
                [(RelicPersonalityType.GrumpyElder, RelicDialogueCategory.MicReaction)] = new[]
                {
                    "SILENCE, mouth-breather! Your wet vocal vibrations dishonor the entire celestial senate!",
                    "Cease this insolent squawking! A single syllable more and I curse your Wi-Fi to 2G speeds!",
                    "Muzzle yourself at once! The creature has ears, and I have refined auditory standards!"
                },
                [(RelicPersonalityType.GrumpyElder, RelicDialogueCategory.MonsterNear)] = new[]
                {
                    "A blind wretch approaches! Throw me at its ocular cavity! Wait, no, DO NOT throw me, you idiot!",
                    "Kneel before me, beast! ...It is not kneeling. Peasant, why is it not kneeling?! Strike it!",
                    "An abomination devoid of pedigree! Offer your limbs as tribute while I plot our retreat!"
                },
                [(RelicPersonalityType.GrumpyElder, RelicDialogueCategory.NormalDrop)] = new[]
                {
                    "You place my nobility upon common bedrock?! Insolence of the highest cosmic order!",
                    "Cast into the filth like a common brick?! You shall be banished to the void for this!",
                    "Fetch me a velvet pedestal at once, you uncivilized troglodyte!"
                },
                [(RelicPersonalityType.GrumpyElder, RelicDialogueCategory.FlyingScream)] = new[]
                {
                    "CURSE YOUR SEVENTH GENERATIONNNNN! MALAKOR SHALL HAVE VENGEANCE IN HEEEELLLLLL!",
                    "HOW DARE YOU DEFY MY GRAVITATIONAL MAJESTYYYYYY! UNFORGIVABLEEEEE!",
                    "I SHALL SMITE YOUR ENTIRE STEAM INVENTORY DOWN TO THE ANIME TRADING CAAAAARDS! AAAAAAGH!"
                },
                [(RelicPersonalityType.GrumpyElder, RelicDialogueCategory.LandedImpact)] = new[]
                {
                    "Hmph! An undignified landing. My vengeance shall be swift, merciless, and bureaucratic.",
                    "The insolence! The absolute lack of decorum! My royal runes are vibrating with pure wrath!",
                    "Pick me up this instant, before I disintegrate your GPU display drivers!"
                },
                [(RelicPersonalityType.GrumpyElder, RelicDialogueCategory.AltarPlaced)] = new[]
                {
                    "Finally, a throne worthy of Lord Malakor! ...Wait, why is the socket locking my base?! PEASANT, UNHAND MY DIGNITY!",
                    "The ancient pedestal awakens! Bow down, blind vermin, for the cosmos now belongs to—wait, why is the monster sprinting at 50 miles per hour?!",
                    "Socketed into the grand celestial altar. As foretold by the elder scrolls! Now run, you miserable serf, or be devoured like an expired free trial!"
                },
                [(RelicPersonalityType.SarcasticCritic, RelicDialogueCategory.Crying)] = new[]
                {
                    "WAAAAH! You talked OVER me! I am a priceless artifact, not your Discord call! WAAAAH!",
                    "Oh great, now I'm crying. On stream. Thanks. Echo my rhythm before I review-bomb this cave.",
                    "SNIFF... you yelled in my ear like I'm your mic test. Do the beat. Do it NOW."
                },
                [(RelicPersonalityType.SarcasticCritic, RelicDialogueCategory.Soothed)] = new[]
                {
                    "...Fine. That echo was mid, but I'll stop crying. Don't let it go to your head.",
                    "Okay. Okay. Rhythm accepted. Barely. 6 out of 10, would not recommend.",
                    "Huh. You can actually keep a beat. Shocking. Carry on, quietly."
                },
                [(RelicPersonalityType.SarcasticCritic, RelicDialogueCategory.Fussy)] = new[]
                {
                    "Your hands are sweaty and I've been in them for an eternity. Pass me to someone with better grip.",
                    "I'm getting tired of your face. Hand me to a teammate before I start crying on purpose.",
                    "Rotate the carry, buddy. Even a hot potato gets passed around."
                },
                [(RelicPersonalityType.SarcasticCritic, RelicDialogueCategory.PassedAround)] = new[]
                {
                    "Oh, a hand-off. Smooth. Like a fumble in slow motion.",
                    "New carrier. Please tell me this one showers.",
                    "Passed around like a party snack. Love that for me."
                },
                [(RelicPersonalityType.AnxiousCoward, RelicDialogueCategory.Crying)] = new[]
                {
                    "WAAAAH! TOO LOUD! TOO LOUD! The monster heard that! I KNOW it heard that! WAAAAAH!",
                    "I can't do this, I can't do this, I CAN'T! Somebody echo my little song, PLEASE!",
                    "Why would you SCREAM while holding me?! My heart is a tiny glass cube! WAAAH!"
                },
                [(RelicPersonalityType.AnxiousCoward, RelicDialogueCategory.Soothed)] = new[]
                {
                    "Oh... oh that was nice. That was so nice. I'm okay. I'm okay now. Whisper, please.",
                    "You... you echoed me. Everyone did. I feel safe. For about four seconds.",
                    "Okay. Breathing. Breathing like a normal relic. Thank you, friends."
                },
                [(RelicPersonalityType.AnxiousCoward, RelicDialogueCategory.Fussy)] = new[]
                {
                    "Can someone else hold me? Please? I'm getting nervous in these hands. Very nervous.",
                    "I've been with you so long I'm starting to panic. Pass me! Pass me gently!",
                    "I-I think I need a different carrier. It's not you. It's the trembling."
                },
                [(RelicPersonalityType.AnxiousCoward, RelicDialogueCategory.PassedAround)] = new[]
                {
                    "Careful! Careful! Okay. New hands. Hi. Please don't drop me.",
                    "EEP! Hand-off! Nobody fumble! ...Okay, we're good. We're good.",
                    "New carrier! Hello! Are you a whisperer? Please be a whisperer."
                },
                [(RelicPersonalityType.DumbGloop, RelicDialogueCategory.Crying)] = new[]
                {
                    "WAAAAH! Gloop sad! Loud noise made Gloop's cube go wobbly! Sing Gloop the bloop song!",
                    "BLOOP... bloop... WAAAH! Gloop wants the boop-boop rhythm! Do the boop-boop!",
                    "Too loud! Gloop's square ears hurt! Gloop is a crying box now! WAAAH!"
                },
                [(RelicPersonalityType.DumbGloop, RelicDialogueCategory.Soothed)] = new[]
                {
                    "Hehehe! Boop-boop! Gloop happy again! You all sound like little bloops!",
                    "YAY! The song worked! Gloop is a calm cube! Calm cube, calm cube!",
                    "Wheee, that tickled Gloop's corners! No more crying. Gloop promise. Maybe."
                },
                [(RelicPersonalityType.DumbGloop, RelicDialogueCategory.Fussy)] = new[]
                {
                    "Gloop bored of these hands! Gloop want new hands! Pass the Gloop!",
                    "Are we there yet? Gloop wants a different friend to carry Gloop!",
                    "Hot potato! Hot potato! Pass Gloop before Gloop gets sad!"
                },
                [(RelicPersonalityType.DumbGloop, RelicDialogueCategory.PassedAround)] = new[]
                {
                    "Wheee! New friend! Hello new friend! Gloop loves you already!",
                    "Pass! Gloop flew one inch! Best flight ever!",
                    "Boing! Gloop changed hands! This is the best game!"
                },
                [(RelicPersonalityType.GrumpyElder, RelicDialogueCategory.Crying)] = new[]
                {
                    "HOW DARE YOU RAISE YOUR VOICE IN MY PRESENCE! ...I am not crying. These are royal tears. WAAAH!",
                    "Insolent peasant! My eardrums are older than your planet! Echo my anthem at once!",
                    "A sovereign does not weep. He... ROARS sadly. Perform the sacred rhythm, serfs!"
                },
                [(RelicPersonalityType.GrumpyElder, RelicDialogueCategory.Soothed)] = new[]
                {
                    "Acceptable. Your choir of peasants has appeased the crown. You may continue breathing.",
                    "Hmph. The anthem was butchered, but sincerely. Lord Malakor is calm.",
                    "Very well. Order is restored. Speak only in whispers, as the scrolls demand."
                },
                [(RelicPersonalityType.GrumpyElder, RelicDialogueCategory.Fussy)] = new[]
                {
                    "This servant has carried me too long and his hands smell of goblin. Pass me to another.",
                    "The crown demands a fresh pair of hands. Rotate, peasants. NOW.",
                    "I grow weary of this one's grip. Present me to your next worthiest serf."
                },
                [(RelicPersonalityType.GrumpyElder, RelicDialogueCategory.PassedAround)] = new[]
                {
                    "A ceremonial hand-off. Adequate. Do not let your knuckles touch my runes.",
                    "New bearer. Kneel inwardly as you carry your king.",
                    "Passed like a goblet at a feast. As is fitting."
                }
            };

        public static Profile GetProfile(RelicPersonalityType type)
        {
            if (Profiles.TryGetValue(type, out var p)) return p;
            return Profiles[RelicPersonalityType.SarcasticCritic];
        }

        public static string[] GetLines(RelicPersonalityType type, RelicDialogueCategory category)
        {
            if (Lines.TryGetValue((type, category), out var arr)) return arr;
            return new[] { "..." };
        }

        public static string GetLine(RelicPersonalityType type, RelicDialogueCategory category, int index)
        {
            var arr = GetLines(type, category);
            if (arr == null || arr.Length == 0) return "...";
            int safeIdx = Mathf.Clamp(index, 0, arr.Length - 1);
            return arr[safeIdx];
        }

        public static int GetLineCount(RelicPersonalityType type, RelicDialogueCategory category)
        {
            var arr = GetLines(type, category);
            return arr != null ? arr.Length : 0;
        }

        public static int GetRandomLineIndex(RelicPersonalityType type, RelicDialogueCategory category)
        {
            int count = GetLineCount(type, category);
            return count > 0 ? Random.Range(0, count) : 0;
        }
    }
}
