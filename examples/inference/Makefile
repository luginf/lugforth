# Concrete runs of the inference engine: each target loads the engine and an example, sets
# some facts, runs `infer`, prints all the facts and checks the expected conclusions.
#
#   make              all the scenarios, then the unit tests
#   make weather-rain-cold | weather-rain | weather-dry | lamp-bulb | lamp-mains | lamp-ok | chain
#   make test         the unit tests only (inference_tests.fs, lamp_diagnosis_tests.fs)
#
# Needs ../../out/posix/lugforth (ninja posix at the repository root).

LUGFORTH ?= ../../out/posix/lugforth
TMPFS    := .scenario.fs

.PHONY: all scenarios test clean weather-rain-cold weather-rain weather-dry lamp-bulb lamp-mains lamp-ok chain

all: scenarios test

scenarios: weather-rain-cold weather-rain weather-dry lamp-bulb lamp-mains lamp-ok chain

# run NAME FILE-TO-INCLUDE "FORTH SETTING THE FACTS" "EXPECTED FACTS AS name=value, space separated"
# (a fact is -1 when true, 0 when false)
define run
	@printf '%s\n' '== $(1)' ; \
	printf 'include inference.fs\ninclude $(2)\nalso facts\n$(3)\ninfer\n.facts\nbye\n' > $(TMPFS) ; \
	$(LUGFORTH) $(TMPFS) 2>&1 | grep ' = ' | tr -d '\r' | sed 's/ *$$//; s/^/   /' | tee .out ; \
	ok=yes ; for e in $(4) ; do \
	  n=$${e%%=*} ; v=$${e##*=} ; \
	  grep -qi "^   $$n = $$v *$$" .out || ok=no ; \
	done ; rm -f $(TMPFS) .out ; \
	if [ $$ok = yes ] ; then echo "   -> OK ($(4))" ; else echo "   -> FAIL, expected: $(4)" ; exit 1 ; fi
endef

weather-rain-cold:
	$(call run,Rain and cold: ice then slippery roads; no umbrella,weather.fs,-1 to rain  -1 to cold,rain=-1 cold=-1 ice=-1 slippery=-1 snow=0 umbrella=0)

weather-rain:
	$(call run,Rain only: take an umbrella,weather.fs,-1 to rain,umbrella=-1 ice=0 snow=0 slippery=0)

weather-dry:
	$(call run,Nothing known: nothing is concluded,weather.fs,,ice=0 snow=0 umbrella=0 slippery=0)

lamp-bulb:
	$(call run,No light but the socket works: change the bulb,lamp_diagnosis.fs,-1 to no-light  -1 to socket-ok,change-bulb=-1 mains-failure=0)

lamp-mains:
	$(call run,No light and the socket is dead: mains failure,lamp_diagnosis.fs,-1 to no-light,change-bulb=0 mains-failure=-1)

lamp-ok:
	$(call run,The lamp works: no conclusion,lamp_diagnosis.fs,-1 to socket-ok,change-bulb=0 mains-failure=0)

# a chain of two rules, defined in the "wrong" order: b-gives-c is tried before a-gives-b
chain:
	@printf '%s\n' '== Chain a -> b -> c, rules defined in reverse order' ; \
	printf 'include inference.fs\nFALSE FACT: a\nFALSE FACT: b\nFALSE FACT: c\n:CONDITION b ; :ACTION TRUE to c ; RULE: b-gives-c\n:CONDITION a ; :ACTION TRUE to b ; RULE: a-gives-b\nalso facts\n-1 to a\ninfer\n.facts\nbye\n' > $(TMPFS) ; \
	$(LUGFORTH) $(TMPFS) 2>&1 | grep ' = ' | tr -d '\r' | sed 's/ *$$//; s/^/   /' | tee .out ; \
	if [ "$$(grep -c ' = -1' .out)" = 3 ] ; then echo "   -> OK (a, b and c are true)" ; r=0 ; else echo "   -> FAIL" ; r=1 ; fi ; \
	rm -f $(TMPFS) .out ; exit $$r

test:
	@echo "== Unit tests"
	@for t in inference_tests.fs lamp_diagnosis_tests.fs ; do \
	  printf '   %-26s' $$t ; \
	  out=$$($(LUGFORTH) $$t 2>&1 | tail -1) ; echo "$$out" ; \
	  case "$$out" in PASS*) ;; *) exit 1 ;; esac ; \
	done

clean:
	rm -f $(TMPFS) .out
