\ Weather: an example for inference.fs (the facts and rules are in the style of
\ Marc Petremann's INFERENCE project, GNU General Public License).
\
\     include examples/inference/inference.fs
\     include examples/inference/weather.fs
\     -1 to rain  -1 to cold  infer  .facts

FALSE FACT: cold
FALSE FACT: rain
FALSE FACT: ice
FALSE FACT: snow
FALSE FACT: umbrella
FALSE FACT: slippery

\ rain and cold: ice on the roads
:CONDITION  rain cold and ;
:ACTION  TRUE to ice ;
RULE: r-ice

\ cold and rain, but no ice: snow
:CONDITION  rain cold and  ice 0= and ;
:ACTION  TRUE to snow ;
RULE: r-snow

\ ice makes the roads slippery
:CONDITION  ice ;
:ACTION  TRUE to slippery ;
RULE: r-slippery

\ rain without cold: take an umbrella
:CONDITION  rain  cold 0= and ;
:ACTION  TRUE to umbrella ;
RULE: r-umbrella
