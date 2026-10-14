/**
 * The astronaut of the intro as a drawing: it shows at once, while the 3D one (AstronautScene.ts) loads, and stays
 * where WebGL is missing. In SVG: a suit lit by the sun from the upper right and by the Earth from below,
 * a gold visor that reflects the Earth's limb, the life-support pack behind, the chest unit, the joint rings and the
 * folds of the fabric. Decoration only (aria-hidden).
 */
import { useId } from 'react';
import styles from './Intro.module.css';

export function Astronaut() {
  // Unique ids: two astronauts on a page (a re-render during the fall) must not share their gradients.
  const id = useId().replace(/[^a-zA-Z0-9]/g, '');
  const ref = (name: string) => `url(#${name}-${id})`;
  const name = (name: string) => `${name}-${id}`;

  const limb = (d: string, width: number) => (
    <path
      d={d}
      stroke={ref('limb')}
      strokeWidth={width}
      className={styles.limb}
      filter={ref('volume')}
    />
  );

  return (
    <svg className={styles.astronaut} viewBox="0 0 240 345" aria-hidden="true">
      <defs>
        <linearGradient id={name('suit')} x1="0.9" y1="0.05" x2="0.1" y2="0.95">
          <stop offset="0" stopColor="#ffffff" />
          <stop offset="0.32" stopColor="#eceef2" />
          <stop offset="0.68" stopColor="#b5bcc8" />
          <stop offset="1" stopColor="#6f7b92" />
        </linearGradient>
        <linearGradient id={name('limb')} x1="1" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#f6f7f9" />
          <stop offset="0.55" stopColor="#ccd1da" />
          <stop offset="1" stopColor="#8a95a8" />
        </linearGradient>
        <linearGradient id={name('pack')} x1="1" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#d9dde4" />
          <stop offset="0.6" stopColor="#9aa3b1" />
          <stop offset="1" stopColor="#545e70" />
        </linearGradient>
        <linearGradient id={name('boot')} x1="1" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#c3c8d1" />
          <stop offset="1" stopColor="#4a5262" />
        </linearGradient>
        <linearGradient id={name('visor')} x1="0.85" y1="0" x2="0.2" y2="1">
          <stop offset="0" stopColor="#fff3cf" />
          <stop offset="0.12" stopColor="#e9c06a" />
          <stop offset="0.38" stopColor="#a8742a" />
          <stop offset="0.7" stopColor="#4a2f0d" />
          <stop offset="1" stopColor="#140d05" />
        </linearGradient>
        <linearGradient id={name('earthshine')} x1="0" y1="0" x2="1" y2="0">
          <stop offset="0" stopColor="#7fc0ff" stopOpacity="0" />
          <stop offset="0.5" stopColor="#9fd0ff" stopOpacity="0.95" />
          <stop offset="1" stopColor="#7fc0ff" stopOpacity="0" />
        </linearGradient>
        <radialGradient id={name('patch')} cx="0.35" cy="0.3" r="0.8">
          <stop offset="0" stopColor="#6d9be0" />
          <stop offset="1" stopColor="#1d3566" />
        </radialGradient>
        <radialGradient id={name('lamp')} cx="0.5" cy="0.5" r="0.5">
          <stop offset="0" stopColor="#fffbe8" />
          <stop offset="0.5" stopColor="#ffe9b0" stopOpacity="0.8" />
          <stop offset="1" stopColor="#ffe9b0" stopOpacity="0" />
        </radialGradient>
        {[
          ['volume', 5],
          ['volumeWide', 9],
        ].map(([kind, depth]) => (
          <filter key={kind} id={name(String(kind))} x="-20%" y="-20%" width="140%" height="140%">
            <feGaussianBlur in="SourceAlpha" stdDeviation={depth} result="height" />
            <feDiffuseLighting
              in="height"
              surfaceScale={Number(depth) * 1.1}
              diffuseConstant="1"
              lightingColor="#ffffff"
              result="shade"
            >
              <feDistantLight azimuth="-48" elevation="42" />
            </feDiffuseLighting>
            <feSpecularLighting
              in="height"
              surfaceScale={Number(depth) * 1.1}
              specularConstant="0.75"
              specularExponent="18"
              lightingColor="#fff4e2"
              result="shine"
            >
              <feDistantLight azimuth="-48" elevation="42" />
            </feSpecularLighting>
            <feComposite in="shine" in2="SourceAlpha" operator="in" result="shineIn" />
            <feComposite
              in="SourceGraphic"
              in2="shade"
              operator="arithmetic"
              k1="1.25"
              k2="0.1"
              result="lit"
            />
            <feComposite in="lit" in2="shineIn" operator="arithmetic" k2="1" k3="0.55" />
          </filter>
        ))}
        <filter id={name('soft')} x="-30%" y="-30%" width="160%" height="160%">
          <feGaussianBlur stdDeviation="2.2" />
        </filter>
        <filter id={name('blur')} x="-50%" y="-50%" width="200%" height="200%">
          <feGaussianBlur stdDeviation="5" />
        </filter>
        <clipPath id={name('visorClip')}>
          <path d="M86 82 C86 60 102 50 121 50 C141 50 156 60 156 82 C156 104 141 116 121 116 C102 116 86 104 86 82 Z" />
        </clipPath>
      </defs>

      {/* The life-support pack, behind everything. */}
      <rect
        x="62"
        y="104"
        width="116"
        height="110"
        rx="16"
        fill={ref('pack')}
        filter={ref('volumeWide')}
      />
      <rect x="66" y="108" width="108" height="10" rx="5" fill="rgb(255 255 255 / 0.18)" />
      <path d="M170 116 v86" stroke="rgb(30 38 52 / 0.35)" strokeWidth="2" />
      <path d="M174 104 l10 -30" stroke="#8d96a5" strokeWidth="2.4" strokeLinecap="round" />
      <circle cx="184" cy="73" r="2.6" fill="#d7dbe2" />

      {/* Legs: thigh and shin, the knee rings, the boots. */}
      {limb('M101 228 Q90 260 84 290', 29)}
      {limb('M84 290 Q87 312 103 324', 26)}
      {limb('M139 228 Q156 254 162 284', 29)}
      {limb('M162 284 Q171 300 186 308', 26)}
      <ellipse
        cx="84"
        cy="290"
        rx="15"
        ry="5.6"
        transform="rotate(-70 84 290)"
        className={styles.ring}
      />
      <ellipse
        cx="162"
        cy="284"
        rx="15"
        ry="5.6"
        transform="rotate(70 162 284)"
        className={styles.ring}
      />
      <path d="M93 258 q8 3 13 -2 M90 270 q8 3 12 -2 M88 304 q7 1 11 -4" className={styles.fold} />
      <path
        d="M148 250 q-7 5 -12 0 M153 262 q-7 5 -12 0 M170 296 q-4 6 -10 4"
        className={styles.fold}
      />
      <path
        d="M95 318 Q110 314 122 324 Q127 334 115 337 L96 335 Q88 331 90 324 Z"
        fill={ref('boot')}
        filter={ref('volume')}
      />
      <path d="M90 333 L117 337" stroke="#262c37" strokeWidth="3.2" strokeLinecap="round" />
      <path
        d="M180 298 Q196 296 206 308 Q208 318 196 318 L180 316 Q172 312 174 306 Z"
        fill={ref('boot')}
        filter={ref('volume')}
      />
      <path d="M174 314 L200 319" stroke="#262c37" strokeWidth="3.2" strokeLinecap="round" />

      {/* The waist: the bearing ring and the lower torso. */}
      <path
        d="M84 204 L156 204 Q162 222 154 238 Q120 250 86 238 Q78 222 84 204 Z"
        fill={ref('suit')}
        filter={ref('volumeWide')}
      />
      <ellipse cx="120" cy="207" rx="41" ry="6.5" className={styles.ring} />
      <path d="M94 226 q26 8 52 0" className={styles.fold} />

      {/* The upper torso. */}
      <path
        d="M78 132 Q76 118 94 116 L146 116 Q164 118 162 132 L164 184 Q164 202 148 207 L92 207 Q76 202 76 184 Z"
        fill={ref('suit')}
        filter={ref('volumeWide')}
      />
      <path d="M88 188 q32 9 64 0" className={styles.fold} />
      <path
        d="M150 124 Q162 150 160 196"
        stroke="rgb(255 246 230 / 0.9)"
        strokeWidth="2.2"
        fill="none"
        filter={ref('soft')}
      />

      {/* The chest unit and its hoses. */}
      <path
        d="M102 172 C90 176 84 186 84 198"
        stroke="#8f99a9"
        strokeWidth="3.2"
        fill="none"
        strokeLinecap="round"
      />
      <path
        d="M138 172 C150 176 156 186 156 198"
        stroke="#8f99a9"
        strokeWidth="3.2"
        fill="none"
        strokeLinecap="round"
      />
      <rect
        x="99"
        y="146"
        width="42"
        height="28"
        rx="4"
        fill="#dfe3e9"
        stroke="#8a94a5"
        strokeWidth="1.2"
      />
      <rect x="104" y="150" width="20" height="9" rx="1.5" fill="#1b2433" />
      <rect x="106" y="152" width="9" height="2" rx="1" fill="#6fd28a" opacity="0.85" />
      <circle cx="132" cy="155" r="3.4" fill="#aeb6c3" stroke="#6b7586" strokeWidth="1" />
      <circle cx="108" cy="167" r="2.6" fill="#9aa3b2" />
      <circle cx="116" cy="167" r="2.6" fill="#9aa3b2" />
      <rect x="124" y="164" width="12" height="5" rx="2" fill="#c96b4a" opacity="0.85" />

      {/* A mission patch on the shoulder. */}
      <circle cx="150" cy="134" r="7" fill={ref('patch')} stroke="#e7d8a6" strokeWidth="1.2" />
      <path
        d="M150 129.5 l1.3 3 3.2 .3 -2.4 2.1 .7 3.1 -2.8 -1.6 -2.8 1.6 .7 -3.1 -2.4 -2.1 3.2 -.3 z"
        fill="#f4ecd0"
      />

      {/* Arms: one raised, one at rest, the elbow and wrist rings, the gloves. */}
      {limb('M82 136 Q58 146 52 170', 25)}
      {limb('M52 170 Q50 188 62 202', 22)}
      {limb('M158 136 Q182 144 192 164', 25)}
      {limb('M192 164 Q200 148 206 132', 22)}
      <ellipse
        cx="52"
        cy="170"
        rx="13"
        ry="5"
        transform="rotate(-80 52 170)"
        className={styles.ring}
      />
      <ellipse
        cx="192"
        cy="164"
        rx="13"
        ry="5"
        transform="rotate(84 192 164)"
        className={styles.ring}
      />
      <ellipse
        cx="62"
        cy="203"
        rx="11.5"
        ry="4.4"
        transform="rotate(-36 62 203)"
        className={styles.ringDark}
      />
      <ellipse
        cx="206"
        cy="131"
        rx="11.5"
        ry="4.4"
        transform="rotate(-62 206 131)"
        className={styles.ringDark}
      />
      <path
        d="M58 206 Q56 218 64 224 Q74 228 78 218 Q80 210 72 205 Z"
        fill={ref('limb')}
        filter={ref('volume')}
      />
      <path
        d="M56 212 q-5 3 -3 9"
        stroke="#c2c8d2"
        strokeWidth="5"
        strokeLinecap="round"
        fill="none"
      />
      <path
        d="M203 126 Q200 114 208 108 Q218 106 220 116 Q222 126 212 130 Z"
        fill={ref('limb')}
        filter={ref('volume')}
      />
      <path
        d="M219 120 q6 -1 7 -8"
        stroke="#d6dbe2"
        strokeWidth="5"
        strokeLinecap="round"
        fill="none"
      />

      {/* The shadow of the helmet on the shoulders. */}
      <ellipse cx="116" cy="124" rx="38" ry="9" fill="rgb(20 28 42 / 0.4)" filter={ref('soft')} />

      {/* The helmet: the shell, its lamps, the gold visor and what it reflects. */}
      <circle cx="120" cy="80" r="47" fill={ref('suit')} filter={ref('volumeWide')} />
      <ellipse cx="120" cy="124" rx="30" ry="7" className={styles.ring} />
      <rect
        x="70"
        y="52"
        width="13"
        height="18"
        rx="3"
        fill="#cfd4dc"
        transform="rotate(-24 76 61)"
      />
      <rect
        x="157"
        y="52"
        width="13"
        height="18"
        rx="3"
        fill="#e9ecf0"
        transform="rotate(24 163 61)"
      />
      <circle cx="166" cy="58" r="9" fill={ref('lamp')} className={styles.lamp} />
      <circle cx="166" cy="58" r="2.6" fill="#fffdf2" />
      <path
        d="M86 82 C86 60 102 50 121 50 C141 50 156 60 156 82 C156 104 141 116 121 116 C102 116 86 104 86 82 Z"
        fill={ref('visor')}
        stroke="#c9ced7"
        strokeWidth="3.4"
      />
      <g clipPath={ref('visorClip')}>
        {/* The Earth's limb and its light, curved by the visor. */}
        <path
          d="M78 112 Q120 88 166 108"
          stroke={ref('earthshine')}
          strokeWidth="5"
          fill="none"
          filter={ref('soft')}
        />
        <path d="M78 122 Q120 96 166 116 L166 130 L78 130 Z" fill="rgb(40 110 200 / 0.55)" />
        <circle cx="140" cy="66" r="0.9" fill="#fff" opacity="0.8" />
        <circle cx="100" cy="88" r="0.7" fill="#fff" opacity="0.6" />
        {/* The sun's highlight and the window of the sky. */}
        <ellipse
          cx="139"
          cy="64"
          rx="14"
          ry="7"
          fill="rgb(255 255 255 / 0.75)"
          transform="rotate(32 139 64)"
          filter={ref('soft')}
        />
        <path
          d="M95 66 Q104 54 120 52"
          stroke="rgb(255 255 255 / 0.6)"
          strokeWidth="3"
          fill="none"
          strokeLinecap="round"
          filter={ref('soft')}
        />
      </g>
      <path
        d="M160 46 Q172 66 168 96"
        stroke="rgb(255 246 230 / 0.95)"
        strokeWidth="2.4"
        fill="none"
        filter={ref('soft')}
      />
      <path
        d="M78 100 Q86 118 104 126"
        stroke="rgb(140 190 255 / 0.5)"
        strokeWidth="3"
        fill="none"
        filter={ref('soft')}
      />
    </svg>
  );
}
