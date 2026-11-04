#!/usr/bin/env python3
"""Offline, read-only summary of exported graphs; never loads game assemblies."""
import argparse
from collections import Counter
import json
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]


class Graph:
    def __init__(self, roots):
        self.roots = roots
        self.ids = {}
        for root in roots:
            self._index(root)

    def _index(self, node):
        if node.get('id'):
            self.ids[node['id']] = node
        for child in node.get('children', []):
            self._index(child)

    def resolve(self, node):
        if node and node.get('referenceId'):
            return self.ids.get(node['referenceId'])
        return node

    def root(self, label):
        return self.resolve(next((n for n in self.roots if n.get('member') == label), None))

    def field(self, node, name):
        node = self.resolve(node)
        if node is None:
            return None
        return self.resolve(next((c for c in node.get('children', [])
                                  if (c.get('member') or '').endswith('.' + name)), None))

    def scalar(self, node):
        node = self.resolve(node)
        if node is None or node.get('status') != 'scalar':
            return None
        return node.get('value')

    def collection(self, node):
        count = self.scalar(self.field(node, '_size'))
        count = int(count) if count is not None else None
        array = self.field(node, '_items')
        items = [self.resolve(n) for n in array.get('children', [])[:count]] if array and count is not None else []
        return items, count


def summarize(entry):
    directory = REPO / entry['path']
    source = json.loads((directory / 'editor-runtime.json').read_text())
    graph = Graph(source['objects'])
    state = graph.root('GameManager.activeDress.state')
    panels, panel_count = graph.collection(graph.field(state, 'panelStates'))
    accessories, accessory_count = graph.collection(graph.field(state, 'accessoryStates'))
    _, join_count = graph.collection(graph.field(state, 'joinStates'))
    def scalar(node, field):
        return graph.scalar(graph.field(node, field))
    panel_rows = []
    for index, panel in enumerate(panels):
        fabric = graph.field(panel, 'fabric')
        panel_rows.append(dict(index=index,
            **{field: scalar(panel, field) for field in
               ['hasBeenCut', 'placedOnMannequin', 'hasBeenStitched', 'grainQuality']},
            fabricStatus=fabric.get('status') if fabric else 'unavailable',
            fabricName=scalar(fabric, 'prettyName'), fabricGuid=scalar(fabric, '_guid')))
    accessory_rows = []
    for index, item in enumerate(accessories):
        definition = graph.field(item, 'definition')
        accessory_rows.append(dict(index=index, placed=scalar(item, 'placed'),
            definitionStatus=definition.get('status') if definition else 'unavailable',
            definitionGuid=scalar(definition, '_guid'), scale=scalar(item, 'scale')))
    return dict(step=entry['step'], userStage=entry['stage'], source=entry['path'],
        timeUtc=source['timeUtc'], unityScene=source['scene'],
        screen=graph.scalar(graph.root('GameManager.<CurrentScene>k__BackingField')),
        dressStateStatus=state.get('status') if state else 'unavailable',
        sewingComplete=scalar(state, 'sewingComplete'), finished=scalar(state, 'finished'),
        panelCount=panel_count, observedPanelCount=len(panels), joinStateCount=join_count,
        accessoryCount=accessory_count, observedAccessoryCount=len(accessories),
        components={name: dict(guid=scalar(graph.field(state, name), '_guid'),
                    prettyName=scalar(graph.field(state, name), 'prettyName'))
                    for name in ['bodiceDefinition', 'sleeveDefinition', 'collarDefinition', 'skirtDefinition']},
        panels=panel_rows, accessories=accessory_rows,
        panelFlagCounts={field: dict(Counter(row[field] if row[field] is not None else 'unknown'
                                            for row in panel_rows))
                         for field in ['hasBeenCut', 'placedOnMannequin', 'hasBeenStitched']},
        limits=source.get('note'),
        graphStatusCounts=dict(Counter(node.get('status') for node in graph.ids.values())))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('manifest', nargs='?', default=str(REPO / 'docs/nine-stage-captures.json'))
    args = parser.parse_args()
    manifest = json.loads(Path(args.manifest).read_text())
    report = dict(analysisVersion='1.0.0', sourceSchema='0.2.0',
                  notice='Derived offline evidence, not a normalized live-game contract. Unknown scalar values stay null; graph limits still apply.',
                  captures=[summarize(entry) for entry in manifest])
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
