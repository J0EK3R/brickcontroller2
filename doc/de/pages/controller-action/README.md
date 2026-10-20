# Controller-Aktion

Eine Controller-Aktion ordnet ein Controller-Ereignis einem Gerätekanal oder einem Gerätemakro zu.

## Allgemeine Optionen

- **Gerät und Kanal**: der Ausgang, den die Aktion steuert.
- **Umkehren**: kehrt die Ausgaberichtung um.
- **Maximale Ausgabe**: begrenzt die Ausgangsleistung. Gilt nur für den normalen Kanaltyp.
- **Tastentyp**: legt fest, wie sich eine Taste verhält (siehe unten).
- **Joystick-Typ**, **Joystick-Merkmal**, **Joystick-Totzone** und **Joystick-Aktivzone**: legen fest, wie sich eine Achse verhält (siehe unten).
- **Makro**: startet eine Gerätefunktion, zum Beispiel die Wiedergabe eines Geräuschs, anstatt einen Kanal anzusteuern.

## Tastentypen

Eine Tastenaktion legt beim Drücken oder Loslassen der Taste die Ausgabe des Kanals fest.

| Tastentyp | Verhalten |
|---|---|
| Normal | Die Ausgabe ist aktiv, solange du die Taste gedrückt hältst, und wird beim Loslassen ausgeschaltet. |
| Einfaches Umschalten | Bei jedem Drücken wechselt die Ausgabe zwischen ein und aus. |
| Abwechselnd | Bei jedem Drücken wird die Ausgabe eingeschaltet, jeweils in der entgegengesetzten Richtung zum vorherigen Mal. |
| Kreisförmig | Bei jedem Drücken wird die Ausgabe schrittweise durch ihren Wertebereich geschaltet; am Ende beginnt sie wieder von vorn. |
| Pingpong | Ein Tastendruck startet die Ausgabe, der nächste stoppt sie und der darauffolgende startet sie in die entgegengesetzte Richtung. |
| Stopp | Stoppt den Kanal und setzt den Zustand aller ihn steuernden Achsen zurück. |
| Beschleuniger | Jeder Tastendruck erhöht die Ausgabe um den Beschleunigungsschritt des Geräts, bis der Grenzwert erreicht ist. |
| Sequenz | Startet oder stoppt die ausgewählte Sequenz auf dem Kanal. |
| Makro | Startet ein Gerätemakro. |

## Achsentypen

Eine Achsenaktion ordnet die Position eines Sticks oder Triggers der Ausgabe zu.

| Joystick-Typ | Verhalten |
|---|---|
| Normal | Die Ausgabe folgt der Position der Achse. |
| Zug | Beim Beschleunigen folgt die Ausgabe der Achse, fällt aber nicht ab, wenn du die Achse zurückbewegst. Das Abbremsen erfolgt allmählich; die Ausgabe bleibt bei null, bis die Achse in die Mitte zurückkehrt. |
| Beschleuniger | Wenn du die Achse am Endanschlag hältst, ändert sich die Ausgabe jeweils um den Beschleunigungsschritt des Geräts. Beim Loslassen behält die Ausgabe ihren Wert. |

Weitere Optionen:

- **Joystick-Totzone**: der mittlere Achsenbereich, der ignoriert wird.
- **Joystick-Aktivzone**: der verwendete Achsenbereich. Am Ende dieses Bereichs wird die volle Ausgabe erreicht.
- **Joystick-Merkmal**: die Reaktionskurve der Achse. Bei einer linearen Kurve ist die Ausgabe proportional zur Bewegung. Eine exponentielle Kurve ermöglicht feinere Steuerung nahe der Mitte, eine logarithmische Kurve erzeugt dort mehr Ausgabe.

## Kanaltypen

Der Kanaltyp legt fest, was die Ausgabe für den angeschlossenen Motor bedeutet. Er ist für Geräte und Kanäle verfügbar, die ihn unterstützen.

| Kanaltyp | Verhalten |
|---|---|
| Normal | Die Ausgabe ist die Motorleistung von -100 % bis +100 %. Sie wird durch **Maximale Ausgabe** begrenzt. |
| Servo | Die Ausgabe bestimmt die Position eines Servos. Lege den **Servobereich** (0 bis 180 Grad) und den **Basiswinkel** (-180 bis 179 Grad) fest. |
| Schrittmotor | Die Ausgabe bewegt einen Schrittmotor in Schritten. Lege mit dem **Schrittwinkel** (0 bis 180 Grad) die Schrittgröße fest. |

Bei den Kanaltypen Servo und Schrittmotor erscheint neben dem Kanaltyp eine Schaltfläche zum Konfigurieren des Kanals.

## Siehe auch

- [Controller-Profil](../controller-profile/README.md)
- [Geräte](../device-list/README.md)
