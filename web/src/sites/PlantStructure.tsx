// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { MetadataCapability, MetadataSite } from "../api/types";
import { Term } from "../components/Term";
import { panel } from "../components/ui";

function Manufacturing({ capability }: { capability: MetadataCapability }) {
  return (
    <article className={panel}>
      <h3 className="text-mps-body font-semibold">{capability.name}</h3>
      <p className="mt-1 break-words text-mps-secondary">{capability.description}</p>
      <ol className="mt-3 space-y-3">
        {capability.lines.map((line) => (
          <li key={line.id}>
            <h4 className="font-semibold">{line.name}</h4>
            <p className="break-words text-mps-secondary">{line.characteristics}</p>
            {line.stepNames.length > 0 ? (
              <p className="mt-1 break-words">In order: {line.stepNames.join(", then ")}.</p>
            ) : null}
            {line.tools.length > 0 ? (
              <ul className="mt-1 list-disc pl-5">
                {line.tools.map((tool) => (
                  <li key={tool.id} className="break-words">
                    {tool.name}
                  </li>
                ))}
              </ul>
            ) : null}
          </li>
        ))}
      </ol>
      {capability.directTools.length > 0 ? (
        <ul className="mt-3 space-y-2">
          {capability.directTools.map((tool) => (
            <li key={tool.id} className="break-words">
              <p className="font-semibold">{tool.name}</p>
              <p>Attached directly to manufacturing, not to a production line.</p>
              {tool.simulated ? null : <p>Not simulated in this demo</p>}
            </li>
          ))}
        </ul>
      ) : null}
    </article>
  );
}

export function PlantStructure({ site, error }: { site: MetadataSite | null; error: string | null }) {
  return (
    <section aria-label="Plant structure" className="mt-6">
      <h2 className="text-mps-title font-semibold">
        Site, <Term id="Capability">capabilities</Term>, <Term id="Production line">production lines</Term>, and tools
      </h2>
      <p className="mt-1 max-w-3xl text-mps-secondary">
        The floor shows where the tools sit. This list is the process order: photoresist strip, clean, thin-film
        deposition, then etch.
      </p>
      {error ? <p className="mt-3 text-mps-alarm-text">{error}</p> : null}
      {!error && !site ? <p className="mt-3 text-mps-muted">Loading the plant structure…</p> : null}
      {site ? (
        <div className="mt-4 space-y-4">
          {site.capabilities.map((capability) =>
            capability.simulated && capability.type === "manufacturing" ? (
              <Manufacturing key={capability.id} capability={capability} />
            ) : (
              <article key={capability.id} className={panel}>
                <h3 className="text-mps-body font-semibold">{capability.name}</h3>
                <p className="mt-1 break-words text-mps-secondary">{capability.description}</p>
                <p className="mt-2">Not simulated in this demo</p>
              </article>
            ),
          )}
        </div>
      ) : null}
    </section>
  );
}
