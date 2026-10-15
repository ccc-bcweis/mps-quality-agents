// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { api, ApiProblem } from "../api/client";
import type { FamilyComparison, MetadataSite, SiteDetail, SiteSummary, ToolFamily } from "../api/types";
import { Term } from "../components/Term";
import { btn, btnPrimary, panel } from "../components/ui";
import { useSetLifecycle } from "../context/ConsoleContext";
import { visibleProblem } from "../history/copy";
import { useSite } from "../context/SiteContext";
import { ComparisonPanel } from "../sites/ComparisonPanel";
import { FloorPlan } from "../sites/FloorPlan";
import { PlantStructure } from "../sites/PlantStructure";
import { formatPct } from "../sites/floorLayout";
import { FAMILY_OPTIONS } from "../sites/parameters";

function isFamily(value: string): value is ToolFamily {
  return FAMILY_OPTIONS.some((option) => option.id === value);
}

function RoleLine({ role }: { role: SiteDetail["role"] }) {
  if (role === "Strip, clean, and pilot line") {
    return (
      <>
        Strip, clean, and <Term id="Pilot line">pilot line</Term>
      </>
    );
  }
  return (
    <>
      Front-end wafer <Term id="Fab">fab</Term>
    </>
  );
}

export function SitesPage() {
  useSetLifecycle("alert");
  const { setSiteId } = useSite();
  const [params, setParams] = useSearchParams();
  const siteId = params.get("site") ?? "northwater";
  const requestedFamily = params.get("family") ?? "etch";
  const family: ToolFamily = isFamily(requestedFamily) ? requestedFamily : "etch";
  const [sites, setSites] = useState<SiteSummary[] | null>(null);
  const [detail, setDetail] = useState<SiteDetail | null>(null);
  const [comparison, setComparison] = useState<FamilyComparison | null>(null);
  const [listError, setListError] = useState<string | null>(null);
  const [detailError, setDetailError] = useState<string | null>(null);
  const [compareError, setCompareError] = useState<string | null>(null);
  const [structure, setStructure] = useState<MetadataSite[] | null>(null);
  const [structureError, setStructureError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    api
      .metadataSites()
      .then((next) => {
        if (!cancelled) setStructure(next);
      })
      .catch(() => {
        if (!cancelled) setStructureError("The plant structure did not load.");
      });
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    let cancelled = false;
    api
      .listSites()
      .then((next) => {
        if (!cancelled) setSites(next);
      })
      .catch((reason: unknown) => {
        if (!cancelled) setListError(reason instanceof ApiProblem ? reason.title : "The plants did not load.");
      });
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    let cancelled = false;
    setDetail(null);
    setDetailError(null);
    api
      .getSite(siteId)
      .then((next) => {
        if (!cancelled) setDetail(next);
      })
      .catch((reason: unknown) => {
        if (!cancelled) setDetailError(reason instanceof ApiProblem ? reason.title : "This plant was not found.");
      });
    return () => {
      cancelled = true;
    };
  }, [siteId]);

  useEffect(() => {
    let cancelled = false;
    setComparison(null);
    setCompareError(null);
    api
      .compareSites(family)
      .then((next) => {
        if (!cancelled) setComparison(next);
      })
      .catch((reason: unknown) => {
        if (!cancelled) setCompareError(reason instanceof ApiProblem ? reason.title : "The comparison did not load.");
      });
    return () => {
      cancelled = true;
    };
  }, [family]);

  function setQuery(key: string, value: string) {
    setParams(
      (prev) => {
        const next = new URLSearchParams(prev);
        next.set(key, value);
        return next;
      },
      { replace: true },
    );
  }

  return (
    <div>
      <p className="font-mono text-mps-micro uppercase tracking-wider text-mps-muted">Three plants</p>
      <h1 className="text-mps-display font-semibold">Sites</h1>
      <p className="max-w-3xl text-mps-secondary">
        A <Term id="Bay" /> is a group of tools that share a process step. A <Term id="Fab">fab</Term> is a plant that runs wafers. Lumen Pilot Works also keeps a <Term id="Pilot line">pilot line</Term> for research wafers.
      </p>
      {visibleProblem(listError) ? <p className="mt-3 text-mps-alarm-text">{visibleProblem(listError)}</p> : null}
      <div role="tablist" aria-label="Plant" className="mt-4 flex flex-wrap gap-2">
        {(sites ?? []).map((site) => {
          const selected = site.id === siteId;
          return (
            <button
              key={site.id}
              type="button"
              role="tab"
              aria-selected={selected}
              className={selected ? btnPrimary : btn}
              onClick={() => {
                setSiteId(site.id);
                setQuery("site", site.id);
              }}
            >
              {site.name}
            </button>
          );
        })}
      </div>
      {visibleProblem(detailError) ? <p className="mt-3 text-mps-alarm-text">{visibleProblem(detailError)}</p> : null}
      {detail ? (
        <>
          <div className="mt-4">
            <h2 className="text-mps-title font-semibold">{detail.name}</h2>
            <p className="text-mps-secondary">
              <RoleLine role={detail.role} />
              <span> · {detail.location}</span>
              <span> · Time zone {detail.timezone}</span>
            </p>
          </div>
          <section aria-label={`${detail.name} throughput`} className={`${panel} mt-3`}>
            <h3 className="mb-2 text-mps-body font-semibold">Plant throughput</h3>
            <dl className="grid grid-cols-2 gap-3 md:grid-cols-5">
              <div>
                <dt className="text-mps-micro text-mps-muted">
                  <Term id="Wafer starts" />
                </dt>
                <dd className="font-mono text-mps-title">{detail.kpis.waferStartsPerHour.toFixed(1)} per hour</dd>
              </div>
              <div>
                <dt className="text-mps-micro text-mps-muted">
                  <Term id="Wafer outs" />
                </dt>
                <dd className="font-mono text-mps-title">{detail.kpis.waferOutsPerHour.toFixed(1)} per hour</dd>
              </div>
              <div>
                <dt className="text-mps-micro text-mps-muted">
                  <Term id="WIP" />
                </dt>
                <dd className="font-mono text-mps-title">{detail.kpis.wip} wafers</dd>
              </div>
              <div>
                <dt className="text-mps-micro text-mps-muted">
                  <Term id="Yield" />
                </dt>
                <dd className="font-mono text-mps-title">{formatPct(detail.kpis.yieldPct)}</dd>
              </div>
              <div>
                <dt className="text-mps-micro text-mps-muted">
                  <Term id="Uptime" />
                </dt>
                <dd className="font-mono text-mps-title">{formatPct(detail.kpis.uptimePct)}</dd>
              </div>
            </dl>
          </section>
          <div className="mt-4">
            <FloorPlan site={detail} />
          </div>
          <ul aria-label="Status lights" className="mt-3 flex flex-wrap gap-4 text-mps-body">
            <li className="inline-flex items-center gap-2">
              <span aria-hidden="true" className="inline-block h-2.5 w-2.5 rounded-full bg-mps-ok" />
              Nominal, in the normal band
            </li>
            <li className="inline-flex items-center gap-2">
              <span aria-hidden="true" className="inline-block h-2.5 w-2.5 rounded-full bg-mps-warn" />
              Watch, drifting
            </li>
            <li className="inline-flex items-center gap-2">
              <span aria-hidden="true" className="inline-block h-2.5 w-2.5 rounded-full bg-mps-alarm" />
              Alarm, a scripted fault
            </li>
          </ul>
          <PlantStructure
            site={structure?.find((item) => item.id === siteId) ?? null}
            error={structureError}
          />
        </>
      ) : detailError ? null : (
        <p className="mt-4 text-mps-muted">Loading the plant…</p>
      )}
      <ComparisonPanel comparison={comparison} family={family} error={compareError} onFamily={(next) => setQuery("family", next)} />
    </div>
  );
}
