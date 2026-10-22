\ Lamp diagnosis: an example for inference.fs (the facts and rules are in the style of
\ Marc Petremann's INFERENCE project, GNU General Public License).
\
\     include examples/utils/inference/inference.fs
\     include examples/utils/inference/lamp_diagnosis.fs
\     also facts  -1 to no-light  -1 to socket-ok  infer  .facts      \ change-bulb = -1

FALSE FACT: no-light         \ what we observe
FALSE FACT: socket-ok
FALSE FACT: change-bulb      \ what we conclude
FALSE FACT: mains-failure

\ no light although the socket works: the bulb is dead
:CONDITION  no-light socket-ok and ;
:ACTION     TRUE to change-bulb ;
RULE: r-bulb

\ no light and the socket is dead: a problem with the mains
:CONDITION  no-light socket-ok 0= and ;
:ACTION     TRUE to mains-failure ;
RULE: r-mains
