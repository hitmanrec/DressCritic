import pathlib, tempfile, subprocess, shutil, json
with tempfile.TemporaryDirectory(prefix='critic-import-') as d:
 root=pathlib.Path(d);helper=root/'repo'/'pull-exports.sh';helper.parent.mkdir();shutil.copy(pathlib.Path(__file__).resolve().parents[1]/'pull-exports.sh',helper)
 game=root/'game'; game.mkdir();(game/'Dressmaker.exe').touch()
 packet=game/'BepInEx/config/DressmakerCritic/captures/fixture'; packet.mkdir(parents=True)
 (packet/'.capture-in-progress').write_text('fixture');(packet/'manifest.json').write_text('{"complete":false}')
 def run(*args):
  subprocess.run(['bash',str(helper),*args,'--no-log',str(game)],check=True,capture_output=True,text=True)
 destination=helper.parent/'DressmakerCritic-Diagnostics/exports/captures/fixture'
 run();assert not destination.exists() and packet.exists()
 run('--all');assert destination.exists() and packet.exists() and (destination/'.capture-in-progress').exists()
 (packet/'.capture-in-progress').unlink();(packet/'manifest.json').write_text('{"complete":true}');(packet/'new.json').write_text('{}')
 run();assert not packet.exists() and (destination/'new.json').exists() and not (destination/'.capture-in-progress').exists()
 assert json.loads((destination/'manifest.json').read_text())['complete']
 print('PASS importer skips active capture, preserves forced partial source, refreshes partial copy, then cleans completed source')
