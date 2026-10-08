# Musical bends and big moments

Keep the current camera, Brain and infinite content. Add **Curved World Automation** to the same object as **CurvedWorldBridge**. It modifies render-time world-bend values only, never the camera or authored Bridge fields. Disabling it immediately returns to the underlying bend settings.

## A large twist for a drop

1. Set the Bridge to the desired shape, e.g. Custom Bend / Twisted Spiral Z Positive. Keep its Amount above zero.
2. Assign the scene's MasterClock to the automation component. Otherwise it auto-finds one; without a clock it uses Fallback BPM and beats per bar.
3. Use a motion targeting Curvature, with Moment Only enabled, Sine wave, One Bar, Low equal to the normal curvature (e.g. 2), and High set to the peak (e.g. 12).
4. In Play mode click **Trigger Moment**. With Quantize Moment To Bar enabled it waits for the next bar; otherwise it begins immediately. Trigger again to reschedule/restart. Stop Moment cancels it.

Sine starts at Low, reaches High halfway through, then returns to Low. Saw starts at High and ramps toward Low. Square holds High for its Duty fraction then switches to Low. Saw/square intentionally have abrupt edges. When the moment completes it releases control back to the current base bend. For smooth sine releases, Low should match the base value.

Periods are Four Bars (4/1), Two Bars (2/1), One Bar (1/1), Half Bar (1/2), Quarter Bar (1/4). In 4/4 these are 16, 8, 4, 2 and 1 beats. The song clock's time signature is respected. Song seeks cancel armed moments; continuous waves follow the new song position. Pausing the song clock pauses the wave. The existing virtual/export clock is supported.

Turn Moment Only off for continuous motion. Add multiple motions for combined twists and sweeps. Targets: curvature [-30,30], horizontal [-15,15], vertical [-10,10], amount [0,1], intensity [0,3]. Bounds apply to automation target values before the normal amount/intensity multipliers. Later motions for the same target blend over earlier ones. Amount modulation multiplies the base amount, so it respects a zero/off base and slot fade-outs. Not all bend shapes use every parameter.

Strength scales each motion's contribution; the component's Amount scales all motions. The automation itself introduces no camera roll. Existing Bridge Spin remains a separate authored setting.

## Bend lineup

Use **User Curved Controller Manager** for saved bend setups. **Add slot from current Bridge settings** captures the preset/custom shape, curvature, horizontal/vertical bend, axis, amount, intensity, spin and pivot behavior. Do this outside Play mode and save the scene to retain slots.

In Play mode use Previous Bend, Next Bend or Selected Bend. The controller fades the old bend out, switches its shape and fades the new one in. The same camera and content remain active. Disabling the manager restores the Bridge values it took over. Automation is shared across bend slots and remains a separate component.

Public automation methods `TriggerMoment()` and `StopMoment()` can be connected to UnityEvents or called by an existing cue system. This change does not add keyboard bindings or automatically create timeline/Macro Mixer trigger assignments.

Compile verification is separate from checking the visual result. Test one-shot completion/cancellation, bar quantization, clock seek/pause, continuous waves, and switching custom bend shapes in the live scene.
