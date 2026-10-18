(() => {
  'use strict';
  const init = () => {
    const $ = id => document.getElementById(id);
    const field = $('field'), layer = $('units'), game = $('game');
    const characters = [
      ['sage', '별빛지기', '보호 · 지원', '가까운 동료에게 보호막을 나누어 줍니다.', 0, 13, 76, 110, 12],
      ['knight', '철문지기', '방어 · 반격', '앞에서 적을 붙잡고 받은 공격을 되돌려 줍니다.', 1, 41, 57, 220, 24],
      ['archer', '초승달 사수', '원거리 · 집중', '안전한 거리에서 표식이 붙은 적을 노립니다.', 2, 20, 55, 100, 34],
      ['automaton', '톱니 파수꾼', '근접 · 연속 공격', '적에게 다가가 연속 타격을 가합니다.', 3, 45, 79, 180, 28],
      ['alchemist', '이슬 연금사', '지원 · 지속 피해', '적의 이동 경로에 연금술 물방울을 뿌립니다.', 4, 27, 77, 120, 19],
      ['enemy-a', '이끼 괴수', '적 · 근접', '가까운 동료를 향해 다가옵니다.', 5, 74, 53, 240, 22],
      ['enemy-b', '가시 괴수', '적 · 돌격', '빠르게 거리를 좁히는 적입니다.', 5, 86, 64, 200, 26],
      ['enemy-c', '늪 괴수', '적 · 근접', '뭉친 원정대를 압박합니다.', 5, 76, 77, 280, 18]
    ];
    const units = characters.map(([id, name, role, description, sprite, x, y, hp, atk], i) => ({
      id, name, role, description, sprite, x, y, hp, atk, maxHp: hp, ally: i < 5, home: { x, y }
    }));
    const rewards = [
      { name: '새벽의 부적', type: '장비', icon: '◈', text: '동료 한 명의 최대 체력이 12 증가합니다.' },
      { name: '함께 걷는 별', type: '전술', icon: '✦', text: '가까운 동료를 보호하는 원정대 전술입니다.' },
      { name: '단단한 결의', type: '훈련', icon: '↑', text: '철문지기의 공격력이 8 증가합니다.' }
    ];
    let phase = 'prep', selected = units[1], drag = null, suppressClick = 0;
    let gold = 18, wave = 4, speed = 1, paused = false, elapsed = 0, frame = 0, lastTime = 0, lastHit = -1;
    let rewardIndex = -1, rewardTaken = false, extraTactic = false, toastTimer, modalOpener;
    const announce = message => {
      clearTimeout(toastTimer); $('toast').textContent = message; $('toast').classList.add('visible');
      toastTimer = setTimeout(() => { $('toast').classList.remove('visible'); $('toast').textContent = ''; }, 2600);
    };
    const position = unit => {
      unit.el.style.left = `${unit.x}%`; unit.el.style.top = `${unit.y}%`;
      unit.el.querySelector('.health-fill').style.width = `${Math.max(0, unit.hp / unit.maxHp * 100)}%`;
      unit.el.classList.toggle('defeated', unit.hp <= 0);
    };
    const range = () => {
      $('range-ring').hidden = phase !== 'prep' || !selected.ally;
      $('range-ring').style.left = `${selected.x}%`; $('range-ring').style.top = `${selected.y}%`;
    };
    const select = unit => {
      selected = unit;
      units.forEach(other => {
        other.el.classList.toggle('selected', other === unit);
        other.el.setAttribute('aria-pressed', String(other === unit));
      });
      $('unit-name').textContent = unit.name; $('unit-role').textContent = unit.role;
      $('unit-description').textContent = unit.description;
      $('unit-hp').textContent = Math.ceil(unit.hp); $('unit-atk').textContent = unit.atk;
      $('unit-portrait').className = `sprite sprite-${unit.sprite}`;
      range();
    };
    const valid = (unit, x, y) => x >= 10 && x <= 49 && y >= 49 && y <= 80 &&
      units.every(other => !other.ally || other === unit || ((other.x - x) / 6) ** 2 + ((other.y - y) / 8) ** 2 >= 1);
    const controls = () => {
      $('start-battle').disabled = phase !== 'prep' || !!drag;
      $('pause-button').disabled = phase !== 'battle';
      $('pause-button').innerHTML = paused ? '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M7 4v16l13-8z" fill="currentColor"/></svg>' : '<svg aria-hidden="true"><use href="#i-pause"/></svg>';
      $('pause-button').setAttribute('aria-label', paused ? '전투 계속하기' : '일시정지');
      $('pause-button').setAttribute('aria-pressed', String(paused));
      $('speed-button').textContent = `${speed}×`;
      $('speed-button').setAttribute('aria-label', `전투 속도 ${speed}배, 눌러 변경`);
      $('gold').textContent = gold; $('wave').textContent = wave;
    };
    const finishDrag = cancel => {
      if (!drag) return;
      const { unit, x, y, pointerId } = drag;
      if (cancel || !valid(unit, unit.x, unit.y)) {
        unit.x = x; unit.y = y;
        if (!cancel) announce('아군 영역 안에서 동료와 겹치지 않는 곳에 놓아 주세요.');
      } else unit.home = { x: unit.x, y: unit.y };
      unit.el.classList.remove('dragging', 'invalid-drop'); drag = null;
      if (unit.el.hasPointerCapture(pointerId)) unit.el.releasePointerCapture(pointerId);
      suppressClick = performance.now() + 150; position(unit); range(); controls();
    };
    units.forEach(unit => {
      const button = document.createElement('button'); unit.el = button;
      button.type = 'button'; button.className = `unit ${unit.ally ? 'ally' : 'enemy'}`;
      button.dataset.unit = unit.id; button.setAttribute('aria-label', `${unit.name}, ${unit.role}`);
      button.innerHTML = `<span class="health-track" aria-hidden="true"><span class="health-fill"></span></span><span class="foot-shadow" aria-hidden="true"></span><span class="sprite sprite-${unit.sprite}" aria-hidden="true"></span><span class="unit-nameplate">${unit.name}</span>`;
      layer.append(button); position(unit);
      button.addEventListener('pointerdown', event => {
        if (event.button !== 0 || drag) return;
        select(unit);
        if (phase !== 'prep' || !unit.ally) return;
        event.preventDefault(); button.focus({ preventScroll: true });
        drag = { unit, x: unit.x, y: unit.y, px: event.clientX, py: event.clientY, pointerId: event.pointerId };
        button.setPointerCapture(event.pointerId); button.classList.add('dragging'); controls();
      });
      button.addEventListener('pointermove', event => {
        if (!drag || drag.unit !== unit || drag.pointerId !== event.pointerId) return;
        const rect = field.getBoundingClientRect();
        unit.x = drag.x + (event.clientX - drag.px) / rect.width * 100;
        unit.y = drag.y + (event.clientY - drag.py) / rect.height * 100;
        button.classList.toggle('invalid-drop', !valid(unit, unit.x, unit.y)); position(unit); range();
      });
      button.addEventListener('pointerup', event => { if (drag?.pointerId === event.pointerId) finishDrag(false); });
      button.addEventListener('pointercancel', event => { if (drag?.pointerId === event.pointerId) finishDrag(true); });
      button.addEventListener('lostpointercapture', () => { if (drag?.unit === unit) finishDrag(true); });
      button.addEventListener('click', () => select(unit));
      button.addEventListener('keydown', event => {
        const delta = { ArrowLeft: [-2, 0], ArrowRight: [2, 0], ArrowUp: [0, -2], ArrowDown: [0, 2] }[event.key];
        if (!delta || phase !== 'prep' || !unit.ally) return;
        event.preventDefault(); select(unit);
        const x = unit.x + delta[0], y = unit.y + delta[1];
        if (!valid(unit, x, y)) return announce('배치 영역의 가장자리이거나 다른 동료와 가까워요.');
        unit.x = x; unit.y = y; unit.home = { x, y }; position(unit); range();
      });
    });
    field.addEventListener('click', event => {
      if (phase !== 'prep' || drag || !selected.ally || performance.now() < suppressClick || event.target.closest('button')) return;
      const rect = field.getBoundingClientRect(), x = (event.clientX - rect.left) / rect.width * 100, y = (event.clientY - rect.top) / rect.height * 100;
      if (!valid(selected, x, y)) return;
      selected.x = x; selected.y = y; selected.home = { x, y }; position(selected); range();
    });
    const setPaused = value => {
      paused = value; controls();
      if (phase === 'battle') $('phase-hint').textContent = paused ? '전투가 멈췄습니다. 계속하기를 눌러 진행하세요.' : '동료들이 자동으로 이동하며 싸웁니다.';
    };
    const renderRewards = () => {
      rewardIndex = -1; rewardTaken = false; $('reward-confirm').disabled = true;
      $('reward-description').textContent = '세 가지 발견 중 하나를 골라 주세요.';
      $('reward-gold').textContent = '+4'; $('reward-options').replaceChildren();
      rewards.forEach((reward, index) => {
        const button = document.createElement('button'); button.type = 'button'; button.className = 'reward-card';
        button.setAttribute('aria-pressed', 'false');
        button.innerHTML = `<span class="reward-icon" aria-hidden="true">${reward.icon}</span><span class="reward-type">${reward.type}</span><strong>${reward.name}</strong><span class="reward-desc">${reward.text}</span>`;
        button.addEventListener('click', () => {
          rewardIndex = index;
          [...$('reward-options').children].forEach((card, i) => { card.classList.toggle('selected', i === index); card.setAttribute('aria-pressed', String(i === index)); });
          $('reward-description').textContent = reward.text; $('reward-confirm').disabled = false;
        });
        $('reward-options').append(button);
      });
    };
    const enter = mode => {
      finishDrag(true); cancelAnimationFrame(frame); frame = 0;
      layer.querySelectorAll('.damage-pop').forEach(pop => pop.remove());
      phase = mode; game.dataset.phase = mode; paused = false;
      [field, document.querySelector('.command-dock'), document.querySelector('.game-hud'), $('scout-button')].forEach(element => { element.inert = mode === 'reward'; });
      $('reward-overlay').hidden = mode !== 'reward'; $('deploy-zone').hidden = mode !== 'prep';
      const copy = {
        prep: ['PREPARE YOUR PARTY', '한 걸음 더, 숲의 안쪽으로', '동료를 끌어 전투를 준비하세요.'],
        battle: ['INTO THE WILD', '원정대가 전진합니다', '동료들이 자동으로 이동하며 싸웁니다.'],
        reward: ['VICTORY', '새로운 발견', '동료와 함께 다음 원정을 준비하세요.']
      }[mode];
      ['phase-kicker', 'phase-title', 'phase-hint'].forEach((id, index) => { $(id).textContent = copy[index]; });
      units.forEach(unit => {
        unit.el.classList.remove('attacking', 'taking-hit');
        if (mode !== 'reward') { unit.x = unit.home.x; unit.y = unit.home.y; unit.hp = unit.maxHp; position(unit); }
      });
      document.querySelectorAll('[data-preview]').forEach(button => button.setAttribute('aria-pressed', String(button.dataset.preview === mode)));
      if (mode === 'reward') renderRewards();
      if (mode === 'battle') { elapsed = 0; lastHit = -1; lastTime = performance.now(); frame = requestAnimationFrame(tick); }
      select(selected); controls();
    };
    const tick = now => {
      const dt = Math.min(now - lastTime, 80); lastTime = now;
      if (phase !== 'battle') return;
      if (!paused) {
        elapsed += dt * speed;
        const progress = Math.min(1, elapsed / 7000), advance = Math.min(1, progress / 0.28);
        units.forEach((unit, index) => {
          const distance = unit.ally ? (unit.id === 'knight' || unit.id === 'automaton' ? 19 : 5) : -12;
          unit.x = unit.home.x + distance * advance;
          unit.hp = unit.maxHp * Math.max(0, 1 - progress * (unit.ally ? (index === 1 ? 0.58 : 0.25) : 1));
          unit.el.classList.toggle('attacking', progress > 0.2 && Math.floor(elapsed / 280 + index) % 4 === 0);
          position(unit);
        });
        const hit = Math.floor(elapsed / 470);
        if (hit !== lastHit && progress > 0.22) {
          lastHit = hit;
          const target = units[5 + hit % 3], pop = document.createElement('span');
          pop.className = 'damage-pop'; pop.textContent = `−${12 + hit % 5 * 3}`;
          pop.style.left = `${target.x}%`; pop.style.top = `${target.y - 14}%`; pop.setAttribute('aria-hidden', 'true');
          layer.append(pop); setTimeout(() => pop.remove(), 650);
        }
        $('unit-hp').textContent = Math.ceil(selected.hp);
        if (progress >= 1) { enter('reward'); return; }
      }
      frame = requestAnimationFrame(tick);
    };
    $('start-battle').addEventListener('click', () => { if (phase === 'prep' && !drag) enter('battle'); });
    $('pause-button').addEventListener('click', () => { if (phase === 'battle') setPaused(!paused); });
    $('speed-button').addEventListener('click', () => { speed = speed === 1 ? 2 : 1; controls(); });
    $('reward-confirm').addEventListener('click', () => {
      if (phase !== 'reward' || rewardIndex < 0 || rewardTaken) return;
      rewardTaken = true; $('reward-confirm').disabled = true;
      const reward = rewards[rewardIndex];
      if (rewardIndex === 0) (selected.ally ? selected : units[1]).maxHp += 12;
      if (rewardIndex === 1) extraTactic = true;
      if (rewardIndex === 2) units[1].atk += 8;
      gold += 4;
      if (wave >= 15) {
        wave = 1; enter('prep');
        modalOpener = $('start-battle'); $('modal-title').textContent = '원정 완료';
        $('modal-body').textContent = '숲의 끝에 도착했습니다. 새로운 원정의 첫 전투가 준비되었습니다.';
        $('modal').hidden = false; $('modal-close').focus();
      } else { wave += 1; enter('prep'); announce(`${reward.name} 획득 · 다음 전투를 준비하세요.`); }
    });
    const closeModal = () => { $('modal').hidden = true; modalOpener?.focus(); };
    $('tactic-button').addEventListener('click', () => {
      modalOpener = document.activeElement; $('modal-title').textContent = '원정 전술 · 보호의 등불';
      $('modal-body').textContent = '보호막이 깨지면 가까운 동료가 반격합니다. 준비 단계에서 별빛지기를 선택하면 보호 범위를 확인할 수 있습니다. 동료를 가까이 모으면 보호하기 쉽지만, 적의 범위 공격에는 주의하세요.';
      if (extraTactic) $('modal-body').textContent += '\n\n새로 얻은 전술 · 함께 걷는 별: 가까운 동료를 보호하는 힘을 더합니다.';
      $('modal').hidden = false; $('modal-close').focus();
    });
    $('modal-close').addEventListener('click', closeModal);
    $('scout-button').addEventListener('click', () => {
      modalOpener = $('scout-button'); $('modal-title').textContent = '적의 특징 · 가시 껍질';
      $('modal-body').textContent = '가시 괴수는 가까운 공격자에게 피해 일부를 되돌립니다. 방어 동료를 앞에 세우고, 원거리 동료는 간격을 두어 배치하세요.';
      $('modal').hidden = false; $('modal-close').focus();
    });
    $('modal').addEventListener('click', event => { if (event.target === $('modal')) closeModal(); });
    document.addEventListener('keydown', event => {
      if (event.key === 'Tab' && !$('modal').hidden) { event.preventDefault(); $('modal-close').focus(); }
      if (event.key === 'Escape' && !$('modal').hidden) closeModal();
      else if (event.key === 'Escape' && drag) finishDrag(true);
    });
    document.addEventListener('visibilitychange', () => { if (document.hidden && phase === 'battle') setPaused(true); });
    document.querySelectorAll('[data-preview]').forEach(button => button.addEventListener('click', () => {
      if (['prep', 'battle', 'reward'].includes(button.dataset.preview)) enter(button.dataset.preview);
    }));
    enter('prep');
  };
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init, { once: true });
  else init();
})();
