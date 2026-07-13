# WebGPU Three.js TSL Skill

An Agent Skill for developing WebGPU-enabled Three.js applications using TSL (Three.js Shading Language).

**Last updated:** April 1, 2026 — aligned with Three.js r183+ API changes.

## Overview

This skill provides Claude with comprehensive knowledge for:

- Setting up Three.js with WebGPU renderer
- Writing shaders using TSL (Three.js Shading Language)
- Creating node-based materials
- Building GPU compute shaders
- Implementing post-processing effects
- Integrating custom WGSL code

## Installation

This repo ships the same content in two formats so it works in both Claude Code (as an Agent Skill) and Cursor (as project rules). The Claude skill under `skills/` is the source of truth; the Cursor rules under `.cursor/rules/` are thin shims that `@file`-reference those same docs, so edits flow through automatically.

### Claude Code

```bash
# Install from this repository
/skill install webgpu-threejs-tsl@<your-github-username>/webgpu-claude-skill
```

Or manually copy the `skills/webgpu-threejs-tsl` folder to:
- **Global**: `~/.claude/skills/`
- **Project**: `<project>/.claude/skills/`

### Cursor

Clone this repo and open it directly — Cursor picks up `.cursor/rules/` automatically.

To use the rules in your own project, copy **both** directories into your project root, preserving the paths:

```
your-project/
├── .cursor/rules/              # from this repo
└── skills/webgpu-threejs-tsl/  # from this repo (referenced by the rules)
```

The `.mdc` files use `@file` references pointing at `skills/webgpu-threejs-tsl/...`, so the `skills/` directory must travel with them. If you'd rather not keep the `skills/` folder, inline the referenced content into each `.mdc` file.

The rules are scoped by globs and auto-attach only on relevant files:

- `webgpu-threejs-tsl.mdc` — entry point, JS/TS files
- `compute-shaders.mdc` — files matching `*compute*` or `*particle*`
- `post-processing.mdc` — files matching `*post*`, `*effect*`, `*bloom*`
- `wgsl-integration.mdc` — `.wgsl` files and `*wgsl*` JS/TS
- `device-loss-and-limits.mdc` — files matching `*renderer*` or `*webgpu*`

## Skill Structure

```
skills/webgpu-threejs-tsl/
├── SKILL.md                    # Entry point with overview
├── REFERENCE.md                # Quick reference cheatsheet
├── docs/
│   ├── core-concepts.md        # Types, operators, uniforms, control flow
│   ├── materials.md            # Node materials and properties
│   ├── compute-shaders.md      # GPU compute documentation
│   ├── post-processing.md      # Built-in and custom effects
│   ├── wgsl-integration.md     # Custom WGSL functions
│   └── device-loss.md          # GPU device loss handling and recovery
├── examples/
│   ├── basic-setup.js          # Minimal WebGPU project
│   ├── custom-material.js      # Custom shader material
│   ├── particle-system.js      # GPU compute particles
│   ├── post-processing.js      # Effect pipeline
│   └── earth-shader.js         # Complete Earth with atmosphere
└── templates/
    ├── webgpu-project.js       # Starter project template
    └── compute-shader.js       # Compute shader template
```

## Topics Covered

### Core Concepts
- Types and constructors (float, vec2, vec3, vec4, color, uniform)
- Vector swizzling
- Operators and math functions
- Control flow (If, Loop, Fn)
- Time and animation

### Materials
- All node material types
- Material properties (color, roughness, metalness, etc.)
- Physical material features (clearcoat, transmission, iridescence)
- Vertex displacement

### Compute Shaders
- Instanced array buffers
- Parallel physics simulation
- Particle systems
- Atomic operations and barriers

### Post-Processing
- Built-in effects (bloom, blur, FXAA, DOF)
- Custom effects with Fn()
- Effect chaining
- Multiple render targets

### WGSL Integration
- Custom WGSL functions with wgslFn()
- Hybrid TSL/WGSL approaches
- Performance optimization

### Device Loss Handling
- Detecting GPU device loss
- Recovery strategies
- Testing with destroy() and Chrome GPU crash
- State preservation and restoration

## Quick Example

```javascript
import * as THREE from 'three/webgpu';
import { color, time, oscSine, normalWorld, cameraPosition, positionWorld, Fn, float } from 'three/tsl';

// WebGPU renderer
const renderer = new THREE.WebGPURenderer();
await renderer.init();

// TSL material with animated fresnel
const material = new THREE.MeshStandardNodeMaterial();

material.colorNode = color(0x0066ff);

material.emissiveNode = Fn(() => {
  const viewDir = cameraPosition.sub(positionWorld).normalize();
  const fresnel = float(1).sub(normalWorld.dot(viewDir).saturate()).pow(3);
  return color(0x00ffff).mul(fresnel).mul(oscSine(time));
})();
```

## Compatibility

- **Recommended Three.js version**: r171+
- **Target browsers**: Chrome 113+, Edge 113+, Firefox (behind flag), Safari (WebGPU preview)

### Version Notes

- **r178+**: `PI2` deprecated (use `TWO_PI`), `transformedNormalView/World` renamed to `normalView/World`
- **r171+**: Stable TSL API, requires `three/webgpu` import map entry

## Resources

- [Three.js TSL Documentation](https://threejs.org/docs/pages/TSL.html)
- [Three.js Shading Language Wiki](https://github.com/mrdoob/three.js/wiki/Three.js-Shading-Language)
- [Three.js WebGPU Examples](https://github.com/mrdoob/three.js/tree/master/examples)
- [WebGPU Best Practices](https://toji.dev/webgpu-best-practices/)
- [Agent Skills Specification](https://github.com/anthropics/skills)

## License

MIT License

Code examples derived from [Three.js](https://github.com/mrdoob/three.js) (MIT License).
