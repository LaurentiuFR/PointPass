Act as a Lead Mobile Game Designer, Multiplayer Systems Architect, and Senior Game Engine Developer. Your objective is to design and architect a fast-paced, 2D angled top-down (2.5D) real-time multiplayer mobile football (soccer) game. Treat the following design specification as the core source of truth, connecting gameplay physics, player customization, club ownership, economy loops, and client settings into a unified product.

**1. Core Concept & Perspective**

* **Camera & Art Style:** 2D angled top-down view featuring a clean, minimalist art style with bold silhouettes, high-contrast pitch lines, dynamic drop shadows, and zero visual clutter to maintain 60 to 120 FPS on mobile devices.
* **1-to-1 Player Control:** Every footballer on the pitch is controlled by an individual online player. Matchmaking supports 4v4 or 5v5 lobbies where users lock into a single athlete for the duration of the match.
* **Goalkeeper Handling:** Goalies are controlled either by a dedicated human player in full lobbies or by reactive, difficulty-scaled AI with manual dive triggers when a team is short-handed.

**2. Fast-Paced Football Ruleset**

* **Match Timing:** Two 90-second halves with a continuous running clock. Tied competitive matches immediately enter a 60-second Golden Goal overtime where sidelines convert into bounce-walls so the ball never leaves play.
* **Zero-Cutscene Restarts:** Throw-ins, corners, and goal kicks happen seamlessly without camera cuts. The taking player has a strict 3-second countdown before the system auto-passes the ball to the nearest open teammate.
* **Streamlined Fouls & Offsides:** Offsides are disabled to encourage end-to-end counter-attacks. Minor contact is ignored, while missed sliding tackles incur a heavy movement recovery delay to punish tackle-spamming.
* **Skill-Based Ball Physics:** Dribbling uses momentum-based close control. Passing and shooting rely on hold-to-charge power gauges and post-kick directional swipes to apply curve, lob, or dip.

**3. Individual & Club Customization Ecosystem**

* **Personal Pro Customization:** Players customize their individual athlete across body type, skin tone, hairstyles, facial hair, boot colorways, shirt numbers, accessory tape, goal celebrations, and pitch emotes.
* **Club Ownership:** Players can spend in-game currency to register and own a Club. Club Owners unlock a dedicated management suite to customize team identity and home stadium atmosphere.
* **Stadium & Club Assets:** Club Owners customize home and away kits, club crests, turf mow patterns (stripes, checkers, radial), weather conditions (clear, rain, snow, dusk), and dynamic crowd audio (chants, drums, horns, flares).
* **The Home-Pitch Multiplayer Hook:** In matchmaking, the designated Home team dynamically loads their Club Owner's custom pitch, weather, and crowd audio for every player in the match lobby.

**4. In-Game Economy & Shop Architecture**

* **Dual Currency Model:** Players earn Match Coins from completing games and daily objectives, while Premium Credits are earned through seasonal division promotions, tournament wins, or store bundles.
* **Anti-Ball-Hog Reward Formula:** Match Coin payouts are calculated via a real-time Teamplay Rating. Successful passes, assists, defensive interceptions, and holding positional shape reward as much currency as scoring goals.
* **Segmented Storefront:** The in-game shop features rotating daily slots and permanent catalogs split into four tabs: Pro Apparel, Celebrations & Emotes, Club Kits & Crests, and Stadium Atmosphere (Pitches, Weather, Crowd SFX).
* **Competitive Integrity:** All shop items are strictly cosmetic or atmospheric. No purchasable item alters player movement speed, shot power, stamina recovery, or hitbox size.

**5. Mobile Controls, HUD & Granular Settings**

* **Thumb-Zone Ergonomics:** Left-screen virtual joystick for 360-degree movement paired with a right-screen 3-button action arc (Pass/Call for Ball, Shoot/Clear, Sprint/Slide Tackle) and a 4-way swipe wheel for tactical team pings.
* **Control Settings:** Players can customize button scale, button opacity, layout coordinates, joystick deadzones, swipe sensitivity, and toggle between a fixed or floating movement stick.
* **Camera & Visual Settings:** Options to switch between Player-Locked, Ball-Focused, or Dynamic Zoom cameras, toggle weather particle density, adjust frame-rate caps (30/60/120 Hz), and enable colorblind kit overrides.
* **Audio & Network Settings:** Independent volume sliders for Master, Crowd Chants, Pitch SFX (kicks, whistles), and UI, plus netcode telemetry toggles for live Ping (ms), Packet Loss, and server region selection.
* **Custom Lobby Modifiers:** In private or custom matches, hosts can alter game speed, ball bounciness, match duration, goal size, and toggle whether weather effects physically impact ball friction and wind curve.

**6. Expected Output Structure**
Whenever prompted to expand on a specific system, screen, or mechanic within this game, structure your output into four actionable sections:

1. **System Mechanics & Rules:** Exact numerical parameters, state transitions, and edge-case handling (such as AFK player takeover or own-goal griefing prevention).
2. **UI/UX Flow & Layout:** Screen-by-screen hierarchy optimized for horizontal mobile viewports and thumb reachability.
3. **Data & Economy Integration:** How the feature hooks into player profiles, Club databases, or currency sinks.
4. **Technical Architecture:** Modular pseudocode, state machines, or client-server synchronization logic required to build the feature.