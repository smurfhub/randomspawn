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
- El punto de inicio de cada jugador se guarda en el mundo (se asigna al entrar por primera vez). Si muere sin cama, reaparece en el mismo sitio.
- Zona protegida de 10 m alrededor del inicio: no se puede construir ni modificar el terreno.
- Si el personaje tiene cama o punto de logout, se usan como siempre.

## Instalación

1. Instala [BepInEx](https://github.com/BepInEx/BepInEx) para Valheim.
2. Copia `RandomSpawn.dll` en `BepInEx/plugins`.
3. En multijugador, instálalo en el servidor y en los clientes.

## Notas

- El inicio se identifica por el nombre del personaje.
