# Auto-rig dei modelli AI (Blender)

`autorig.py` prende un modello umanoide **statico in A-pose** (es. export di Tripo 3D: FBX, guarda -Y, Z in alto) e produce un FBX pronto per Unity:

- riduzione dei triangoli (`target_tris`) e shading smooth;
- scheletro con i nomi stile Mixamo senza prefisso (`Hips`, `Spine`, `Spine1`, `Spine2`, `Neck`, `Head`, `LeftShoulder`, `LeftArm`, `LeftForeArm`, `LeftHand`, dita `Index/Middle/Thumb 1-3`, `LeftUpLeg`, `LeftLeg`, `LeftFoot`, `LeftToeBase`, e i `Right*`), cioè quelli che cerca `PlayerCharacterBuilder` (`Setup/9`);
- giunti trovati dalla forma della mesh (inforcatura, sezioni di gambe e braccia, testa);
- pesi geometrici (distanza dai segmenti delle ossa, 1/d^4, lisciati lungo i lati della mesh, max 4 ossa per vertice): i pesi automatici di Blender (bone heat) falliscono su queste mesh;
- con `mode=runner` aggiunge le clip procedurali `Idle` (loop), `Run` (loop, 0,6 s), `Attack` (0,7 s), `Death` (1,2 s);
- anteprime PNG (riposo e alcuni fotogrammi) e `<prefisso>_joints.json` con le posizioni dei giunti.

Uso (Blender come modulo Python, `pip install bpy`, Python 3.11):

```
python -I tools/autorig/autorig.py -- in.fbx out.fbx 15000 runner /tmp/anteprima
python -I tools/autorig/autorig.py -- in.fbx out.fbx 40000 main   /tmp/anteprima
```

Poi: Runner in `Assets/ThirdParty/_Own/Runner/Runner.fbx` + `Setup/6`; personaggio in `Assets/ThirdParty/_Own/Pilot/Pilot.fbx` + texture `Pilot_*.png` + `Setup/9` (vedi `docs/ASSETS.md`).

Corsa da motion capture: aggiungi come sesto argomento un BVH della CMU (conversione MotionBuilder 2010, es. `127_08.bvh` da github.com/una-dinosauria/cmu-mocap). Il lettore è `bvh.py`; lo script sceglie da solo un ciclo, lo rende un loop senza cuciture e stampa la velocità della clip (da mettere in `RunClipSpeed` di `EnemyContentBuilder`, moltiplicata per la scala del modello in Unity).

Animazioni da FBX Mixamo ("Without Skin", FBX Binary, 30 fps; le corse senza "In Place"): come sesto argomento un JSON con l'elenco delle clip `{"name", "file", "hips": "loop|pin|keep", "extra"}` (vedi `mixamo_runner_clips.json`). Lo script stampa la velocità di ogni clip (unità del modello al secondo; in Unity x la scala del modello, 1,799 per il Runner).
