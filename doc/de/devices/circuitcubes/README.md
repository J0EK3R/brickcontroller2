# Circuit Cubes

Circuit Cubes ist ein Bluetooth-LE-Motorsteuerungswürfel, der bis zu drei Motoren antreibt.

![Circuit Cubes](../../../../BrickController2/BrickController2/UI/Images/circuitcubes_image.png)

## Kanäle

Das Gerät stellt drei Kanäle bereit:

| Kanäle | Zweck | Ausgabe |
|---|---|---|
| A–C | Motoren | -100 % bis +100 % |

## Erste Schritte

1. Schalte den Bluetooth-Würfel von Circuit Cubes ein.
2. Öffne die Geräteliste und suche nach Geräten.
3. Wähle Circuit Cubes in den Suchergebnissen aus, um das Gerät hinzuzufügen.
4. Verwende das Gerät in einem Modell und weise seine Kanäle Controller-Aktionen zu.

## Hinweise

- Bei der ersten Verwendung verbindet sich das Gerät nicht automatisch. Stelle die Verbindung über die Geräteseite oder durch Starten eines Modells her, das dieses Gerät verwendet.
- Das Gerät meldet die Akkuspannung; sie wird auf der Geräteseite angezeigt.
- Nach jeder Änderung werden Ausgabewerte kurze Zeit wiederholt gesendet, damit der Würfel sie sicher empfängt.

## Siehe auch

- [Geräte](../../pages/device-list/README.md)
- [Controller-Aktion](../../pages/controller-action/README.md)
