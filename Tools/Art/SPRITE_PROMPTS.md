# Polished sprite provenance

Created with the built-in ImageGen tool. Transparent alpha is preserved. Original project sprites remain unchanged. The engine imports these sibling assets at a maximum dimension of 512 pixels through `ArtImportSettings`; the source PNGs retain their generated resolution.

| Output | Reference | Purpose |
| --- | --- | --- |
| `Assets/_Project/Resources/Art/player_blue_polished.png` | Original `player_blue.png` | New overhead torso |
| `Assets/_Project/Resources/Art/player_red_polished.png` | Generated blue torso | Matching red kit |
| `Assets/_Project/Resources/Art/boot_polished.png` | Original `boot.png` | Readable football footwear |
| `Assets/_Project/Resources/Art/ball_polished.png` | Original `ball.png` | Shaded ivory/navy ball; see [ball prompt](BALL_PROMPT.md) |

## Blue player prompt

Use case: stylized-concept. Asset type: production 2D mobile football game torso sprite on genuine transparent RGBA background, square canvas. Reference image is the existing crude blue player, only to establish the strict overhead camera and blue team. Redesign it into a premium clean stylized top-down footballer. CAMERA IS STRICTLY VERTICAL OVERHEAD, looking at the crown of the head and shoulders, footballer FACING STRAIGHT TOWARD THE TOP OF THE IMAGE (+Y). Not front view, not portrait, no visible face or eyes. One centered compact athletic upper body only, torso shoulders arms and head. Athletic royal/cobalt blue jersey, navy side panels, narrow ivory sleeve cuffs, tailored collar, subtle seams, two short arms with medium tan skin and small hands beside shoulders angled slightly forward. Small carefully shaded head and sculpted short dark hair crown, ears subtly visible. Shoulders form an organic athletic silhouette rather than a wide oval. Modern polished mobile game art: restrained 2.5D volume, crisp shapes and slim dark navy contour, upper-left soft light, two or three levels of shading, strong readability at 40px. No legs, no feet, no boots, no football, no separate shadow, no pitch, no text, no badges, no UI, no border, no checkerboard. Separate animated boots will be layered underneath by the game. Uniform proportions and centered pivot, symmetrical neutral running-ready pose, silhouette occupies approximately 85 percent of canvas width and 80 percent height with clean transparent padding. Preserve actual alpha.

## Red player edit prompt

Precise sprite variant edit. Keep the input overhead football torso sprite EXACTLY the same geometry, overhead viewpoint, crop, transparency, pose, head size and position, dark hair, tan skin, arms, shadows, seam design, ivory sleeve cuffs, contour and pixel alignment. Change ONLY the blue fabric colours to a rich vermilion/crimson RED football jersey, with darker burgundy side panels and red shoulder highlights. No other changes, no text, no legs, no feet, no shadow, no background. This is the red team variant of the same player used in a rotating top-down football game. Preserve genuinely transparent alpha and identical canvas and silhouette.

## Boot prompt

Production 2D top-down football boot sprite replacing the reference plain oval. One SINGLE slim athletic soccer cleat viewed strictly from overhead, toe pointing straight UP, heel down. Dark midnight navy upper, slightly squared rounded toe, tiny pale mint trim along the outside sole, three small subdued lace strokes over the midfoot. Crisp premium stylized mobile-game shading with a narrow charcoal outline and restrained highlight on toe. Make it look like real football footwear at small game scale, not an eye or a button: NO large white spot or circle. Centered vertically with transparent padding, sole silhouette slim and elongated around 1:1.8 width to length. No leg, no sock, no foot/skin, no person, no pair, no ball, no ground, no cast shadow, no text, no branding. Genuinely transparent alpha, square image canvas.
