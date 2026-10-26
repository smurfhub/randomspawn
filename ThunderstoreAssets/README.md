# RandomSpawn

Mod de Valheim para BepInEx que cambia el lugar donde aparece un personaje nuevo: en vez del punto fijo de siempre, cada jugador empieza en una zona tranquila y distinta del mapa.

## Qué hace

- **Spawn aleatorio** entre 300 y 3500 metros del centro del mundo.
- **Siempre en Meadows**, a un mínimo de 350 metros de cualquier otro bioma.
- **Cerca de una estructura**, con esta preferencia:
  1. Círculo de piedras
  2. Runestone sin jabalíes
  3. Runestone con jabalíes
  4. Cualquier otra estructura
- Dos jugadores no empiezan en la misma estructura.
- El punto de respawn se guarda en el personaje. Si muere sin cama, reaparece en el mismo sitio.
- Si el personaje tiene cama o punto de logout, se usan como siempre.

## Instalación

1. Instala [BepInEx](https://github.com/BepInEx/BepInEx) para Valheim.
2. Copia `RandomSpawn.dll` en `BepInEx/plugins`.
3. En multijugador, instálalo en el servidor y en los clientes.

## Notas

- Los puntos ya usados por otros jugadores se recuerdan solo mientras el servidor está en marcha.
- Un personaje que entra en otro mundo empieza de nuevo con un inicio nuevo.
